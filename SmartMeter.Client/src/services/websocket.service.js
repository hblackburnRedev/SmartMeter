/**
 * WebSocket Service for Smart Meter Client
 * Handles server communication with query parameter authentication
 */

import { createLogger } from '../utils/logger.js';
import { CONFIG } from '../../config/config.js';
import { storageService } from './storage.service.js';
import { parseDecimal, sleep } from '../utils/helpers.js';

const logger = createLogger('WebSocketService');

/**
 * WebSocket Service Class
 * Manages WebSocket connection and communication with server
 */
class WebSocketService {
    constructor() {
        this.ws = null;
        this.isConnecting = false;
        this.shouldReconnect = true;
        this.reconnectAttempt = 0;

        logger.info('WebSocket service initialized');
    }

    /**
     * Connect to WebSocket server with authentication via query parameters
     * @returns {Promise<void>}
     */
    async connect() {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            logger.warn('Already connected to server');
            return;
        }

        if (this.isConnecting) {
            logger.warn('Connection attempt already in progress');
            return;
        }

        const meterId = storageService.getMeterId();
        if (!meterId) {
            logger.error('Cannot connect - meter ID not set');
            throw new Error('Meter ID not initialized');
        }

        this.isConnecting = true;
        storageService.setConnectionStatus(false);

        try {
            // Build WebSocket URL with query parameters for authentication
            // Browser WebSocket doesn't support custom headers, so we use query params
            const url = new URL(CONFIG.SERVER.URL);
            url.searchParams.append('clientId', meterId);
            url.searchParams.append('apiKey', CONFIG.AUTH.API_KEY);

            logger.info(`Connecting to ${CONFIG.SERVER.URL}`);
            logger.debug(`Using ClientId: ${meterId}`);
            logger.debug(`Full URL: ${url.toString()}`);

            // Create WebSocket connection with auth in URL
            this.ws = new WebSocket(url.toString());

            // Set up event handlers
            this.setupEventHandlers();

            // Wait for connection to open
            await this.waitForConnection();

        } catch (error) {
            logger.error('Connection failed', error);
            this.isConnecting = false;
            storageService.incrementConnectionAttempts();
            storageService.setError(`Connection failed: ${error.message}`);

            // Attempt reconnection
            this.handleReconnection();
            throw error;
        }
    }

    /**
     * Wait for WebSocket connection to open
     * @returns {Promise<void>}
     */
    waitForConnection() {
        return new Promise((resolve, reject) => {
            const timeout = setTimeout(() => {
                reject(new Error('Connection timeout'));
            }, 10000); // 10 second timeout

            this.ws.onopen = () => {
                clearTimeout(timeout);
                this.isConnecting = false;
                this.reconnectAttempt = 0;
                storageService.setConnectionStatus(true);
                logger.info('WebSocket connection established');
                resolve();
            };

            this.ws.onerror = (error) => {
                clearTimeout(timeout);
                this.isConnecting = false;
                logger.error('WebSocket connection error', error);
                reject(new Error('WebSocket connection error'));
            };
        });
    }

    /**
     * Set up WebSocket event handlers
     */
    setupEventHandlers() {
        this.ws.onmessage = (event) => {
            this.handleMessage(event);
        };

        this.ws.onclose = (event) => {
            this.handleClose(event);
        };

        this.ws.onerror = (error) => {
            logger.error('WebSocket error', error);
            storageService.setError('WebSocket connection error');
        };
    }

    /**
     * Handle incoming WebSocket messages
     * @param {MessageEvent} event - WebSocket message event
     */
    handleMessage(event) {
        try {
            logger.debug('Message received', event.data);

            // Server sends decimal as plain string (e.g., "36.12")
            const billAmount = parseDecimal(event.data, 0);

            if (billAmount > 0) {
                logger.info(`Bill update received: £${billAmount.toFixed(2)}`);
                storageService.updateBill(billAmount);
                storageService.clearError();
            } else {
                logger.warn('Received invalid bill amount', event.data);
            }

        } catch (error) {
            logger.error('Error handling message', error);
            storageService.setError(`Message handling error: ${error.message}`);
        }
    }

    /**
     * Handle WebSocket connection close
     * @param {CloseEvent} event - Close event
     */
    handleClose(event) {
        logger.info(`WebSocket closed: Code=${event.code}, Reason=${event.reason || 'None'}`);

        storageService.setConnectionStatus(false);

        // Normal closure
        if (event.code === 1000) {
            logger.info('Normal closure - not reconnecting');
            this.shouldReconnect = false;
            return;
        }

        // Unauthorized
        if (event.code === 1008) {
            logger.error('Unauthorized - check API key');
            storageService.setError('Unauthorized: Invalid API key or Client ID');
            this.shouldReconnect = false;
            return;
        }

        // Unexpected closure - attempt reconnection
        storageService.setError('Connection lost. Reconnecting...');
        this.handleReconnection();
    }

    /**
     * Handle reconnection logic
     */
    async handleReconnection() {
        if (!this.shouldReconnect) {
            logger.info('Reconnection disabled');
            return;
        }

        if (this.reconnectAttempt >= CONFIG.SERVER.MAX_RECONNECT_ATTEMPTS) {
            logger.error('Max reconnection attempts reached');
            storageService.setError('Unable to connect. Maximum attempts reached.');
            this.shouldReconnect = false;
            return;
        }

        this.reconnectAttempt++;
        storageService.incrementConnectionAttempts();

        const delay = CONFIG.SERVER.RECONNECT_DELAY;
        logger.info(`Reconnection attempt ${this.reconnectAttempt}/${CONFIG.SERVER.MAX_RECONNECT_ATTEMPTS} in ${delay}ms`);

        await sleep(delay);

        try {
            await this.connect();
        } catch (error) {
            logger.error('Reconnection failed', error);
        }
    }

    /**
     * Send a meter reading to the server
     * @param {number} reading - Current meter reading in kWh
     * @returns {Promise<void>}
     */
    async sendReading(reading) {
        if (!this.isConnected()) {
            logger.warn('Cannot send reading - not connected');
            storageService.setError('Cannot send reading - not connected to server');
            return;
        }

        try {
            const message = {
                Region: storageService.getRegion(),
                Usage: reading
            };

            logger.info(`Sending reading: ${reading} kWh for region ${message.Region}`);
            await this.send(message);

        } catch (error) {
            logger.error('Failed to send reading', error);
            storageService.setError(`Failed to send reading: ${error.message}`);
            throw error;
        }
    }

    /**
     * Send a message via WebSocket
     * @param {Object} message - Message object to send
     * @returns {Promise<void>}
     */
    send(message) {
        return new Promise((resolve, reject) => {
            if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
                reject(new Error('WebSocket not connected'));
                return;
            }

            try {
                const json = JSON.stringify(message);
                this.ws.send(json);
                logger.debug('Message sent', message);
                resolve();
            } catch (error) {
                logger.error('Error sending message', error);
                reject(error);
            }
        });
    }

    /**
     * Check if connected to server
     * @returns {boolean} Connection status
     */
    isConnected() {
        return this.ws && this.ws.readyState === WebSocket.OPEN && storageService.isConnected();
    }

    /**
     * Disconnect from server
     */
    disconnect() {
        logger.info('Disconnecting from server');

        this.shouldReconnect = false;

        if (this.ws) {
            if (this.ws.readyState === WebSocket.OPEN) {
                this.ws.close(1000, 'Client disconnecting');
            }
            this.ws = null;
        }

        storageService.setConnectionStatus(false);
        logger.info('Disconnected');
    }

    /**
     * Get connection state
     * @returns {string} Connection state (CONNECTING, OPEN, CLOSING, CLOSED)
     */
    getConnectionState() {
        if (!this.ws) return 'CLOSED';

        const states = {
            [WebSocket.CONNECTING]: 'CONNECTING',
            [WebSocket.OPEN]: 'OPEN',
            [WebSocket.CLOSING]: 'CLOSING',
            [WebSocket.CLOSED]: 'CLOSED'
        };

        return states[this.ws.readyState] || 'UNKNOWN';
    }
}

// Export singleton instance
export const websocketService = new WebSocketService();