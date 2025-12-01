import { createLogger } from '../utils/logger.js';
import { CONFIG } from '../../config/config.js';
import { storageService } from './storage.service.js';
import { sleep } from '../utils/helpers.js';

const logger = createLogger('WebSocketService');

class WebSocketService {
    constructor() {
        this.ws = null;
        this.isConnecting = false;
        this.shouldReconnect = true;
        this.reconnectAttempt = 0;
        this.isRegistered = false;
        this.clientName = null;
        this.clientAddress = null;

        logger.info('WebSocket service initialized');
    }

    setClientDetails(name, address) {
        this.clientName = name;
        this.clientAddress = address;
        logger.info(`Client details set: ${name}, ${address}`);
    }

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
            const url = new URL(CONFIG.SERVER.URL);
            url.searchParams.append('clientId', meterId);
            url.searchParams.append('apiKey', CONFIG.AUTH.API_KEY);

            logger.info(`Connecting to ${CONFIG.SERVER.URL}`);

            this.ws = new WebSocket(url.toString());

            this.setupEventHandlers();

            await this.waitForConnection();

            if (!this.isRegistered) {
                await this.sendRegistration();
            }

        } catch (error) {
            logger.error('Connection failed', error);
            this.isConnecting = false;
            storageService.incrementConnectionAttempts();
            storageService.setError(`Connection failed: ${error.message}`);

            this.handleReconnection();
            throw error;
        }
    }

    waitForConnection() {
        return new Promise((resolve, reject) => {
            const timeout = setTimeout(() => {
                reject(new Error('Connection timeout'));
            }, 10000);

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

    async sendRegistration() {
        if (!this.clientName || !this.clientAddress) {
            logger.error('Cannot register - client details not set');
            throw new Error('Client details not initialized');
        }

        try {
            const message = {
                name: this.clientName,
                address: this.clientAddress
            };

            logger.info(`Sending registration: ${this.clientName}`);
            await this.send(message);

        } catch (error) {
            logger.error('Failed to send registration', error);
            throw error;
        }
    }

    handleMessage(event) {
        try {
            logger.debug('Message received', event.data);

            const data = JSON.parse(event.data);

            if (data.status) {
                this.handleGridAlert(data);
            } else if (data.clientId && data.name) {
                this.handleRegistrationResponse(data);
            } else if (data.total !== undefined) {
                this.handleBillUpdate(data);
            } else {
                logger.warn('Unknown message format', data);
            }

        } catch (error) {
            logger.error('Error handling message', error);
            storageService.setError(`Message handling error: ${error.message}`);
        }
    }

    handleRegistrationResponse(data) {
        logger.info(`Registration confirmed: ${data.clientName}`);
        this.isRegistered = true;
        storageService.clearError();
    }

    handleBillUpdate(data) {
        const billAmount = data.total;

        if (typeof billAmount === 'number' && billAmount >= 0) {
            logger.info(`Bill update received: £${billAmount.toFixed(2)}`);
            storageService.updateBill(billAmount);
            storageService.clearError();
        } else {
            logger.warn('Received invalid bill amount', data);
        }
    }

    handleGridAlert(data) {
        const status = data.status.toLowerCase();
        const message = status === 'down'
            ? '⚠️ Grid Alert: Electricity supply disrupted'
            : '✓ Grid Restored: Electricity supply normal';

        logger.warn(`Grid status: ${status}`);
        storageService.setAlert(message);
    }

    handleClose(event) {
        logger.info(`WebSocket closed: Code=${event.code}, Reason=${event.reason || 'None'}`);

        storageService.setConnectionStatus(false);
        this.isRegistered = false;

        if (event.code === 1000) {
            logger.info('Normal closure - not reconnecting');
            this.shouldReconnect = false;
            return;
        }

        if (event.code === 1008) {
            logger.error('Unauthorized - check API key');
            storageService.setError('Unauthorized: Invalid API key or Client ID');
            this.shouldReconnect = false;
            return;
        }

        storageService.setError('Connection lost. Reconnecting...');
        this.handleReconnection();
    }

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

    async sendReading(reading) {
        if (!this.isConnected()) {
            logger.warn('Cannot send reading - not connected');
            storageService.setError('Cannot send reading - not connected to server');
            return;
        }

        if (!this.isRegistered) {
            logger.warn('Cannot send reading - not registered');
            storageService.setError('Cannot send reading - registration pending');
            return;
        }

        try {
            const message = {
                region: storageService.getRegion(),
                usage: reading
            };

            logger.info(`Sending reading: ${reading} kWh for region ${message.region}`);
            await this.send(message);

        } catch (error) {
            logger.error('Failed to send reading', error);
            storageService.setError(`Failed to send reading: ${error.message}`);
            throw error;
        }
    }

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

    isConnected() {
        return this.ws && this.ws.readyState === WebSocket.OPEN && storageService.isConnected();
    }

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
        this.isRegistered = false;
        logger.info('Disconnected');
    }

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

export const websocketService = new WebSocketService();