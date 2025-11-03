/**
 * Renderer - UI Controller for Smart Meter Client
 * Coordinates services and updates the DOM
 */

import { CONFIG, generateMeterId, getRandomRegion, validateConfig } from '../../config/config.js';
import { createLogger } from '../utils/logger.js';
import { formatCurrency, formatReading } from '../utils/helpers.js';
import { storageService } from '../services/storage.service.js';
import { meterService } from '../services/meter.service.js';
import { websocketService } from '../services/websocket.service.js';

const logger = createLogger('Renderer');

/**
 * Application class - main controller
 */
class Application {
    constructor() {
        this.meterId = null;
        this.region = null;
        this.initialized = false;

        // DOM elements (will be populated on init)
        this.elements = {
            status: null,
            reading: null,
            bill: null,
            error: null,
            errorText: null,
            alert: null,
            alertText: null,
            meterId: null
        };
    }

    /**
     * Initialize the application
     */
    async init() {
        try {
            logger.info('Initializing Smart Meter Client');

            // Validate configuration
            validateConfig();

            // Generate unique meter ID and select region
            this.meterId = generateMeterId();
            this.region = getRandomRegion();

            logger.info(`Meter ID: ${this.meterId}`);
            logger.info(`Region: ${this.region}`);

            // Initialize DOM elements
            this.initializeDOMElements();

            // Initialize storage service
            storageService.initialize(this.meterId, this.region);

            // Subscribe to state changes
            this.subscribeToEvents();

            // Update initial UI
            this.updateUI();

            // Connect to server
            await this.connectToServer();

            // Start meter service
            this.startMeterService();

            this.initialized = true;
            logger.info('Application initialized successfully');

        } catch (error) {
            logger.error('Initialization failed', error);
            this.showError(`Initialization failed: ${error.message}`);
        }
    }

    /**
     * Initialize DOM element references
     */
    initializeDOMElements() {
        this.elements = {
            status: document.getElementById('status'),
            reading: document.getElementById('reading'),
            bill: document.getElementById('bill'),
            error: document.getElementById('error'),
            errorText: document.getElementById('error-text'),
            alert: document.getElementById('alert'),
            alertText: document.getElementById('alert-text'),
            meterId: document.getElementById('meter-id'),
            gaugeProgress: document.getElementById('gauge-progress')
        };

        // Verify all elements exist
        for (const [key, element] of Object.entries(this.elements)) {
            if (!element) {
                throw new Error(`DOM element not found: ${key}`);
            }
        }

        // Calculate gauge circumference for animations
        this.gaugeCircumference = 2 * Math.PI * 85; // radius = 85
        this.maxReading = 10; // Maximum reading for gauge (10 kWh)

        logger.debug('DOM elements initialized');
    }

    /**
     * Subscribe to storage service events
     */
    subscribeToEvents() {
        // Reading updates
        storageService.subscribe('reading', (reading) => {
            this.updateReading(reading);
        });

        // Bill updates
        storageService.subscribe('bill', (bill) => {
            this.updateBill(bill);
        });

        // Connection status changes
        storageService.subscribe('connection', (isConnected) => {
            this.updateConnectionStatus(isConnected);
        });

        // Error messages
        storageService.subscribe('error', (error) => {
            this.showError(error);
        });

        // Alert messages
        storageService.subscribe('alert', (alert) => {
            this.showAlert(alert);
        });

        logger.debug('Event subscriptions established');
    }

    /**
     * Connect to WebSocket server
     */
    async connectToServer() {
        try {
            logger.info('Connecting to server...');
            await websocketService.connect();
            logger.info('Connected to server successfully');
        } catch (error) {
            logger.error('Failed to connect to server', error);
            this.showError(`Connection failed: ${error.message}`);
            throw error;
        }
    }

    /**
     * Start the meter service
     */
    startMeterService() {
        logger.info('Starting meter service...');

        // Start meter with callback to send readings via WebSocket
        meterService.start(async (reading) => {
            try {
                await websocketService.sendReading(reading);
            } catch (error) {
                logger.error('Failed to send reading', error);
            }
        }, 2000); // 2 second initial delay

        logger.info('Meter service started');
    }

    /**
     * Update the entire UI
     */
    updateUI() {
        const state = storageService.getState();

        this.updateMeterId(state.meterId);
        this.updateReading(state.currentReading);
        this.updateBill(state.currentBill);
        this.updateConnectionStatus(state.isConnected);
    }

    /**
     * Update meter ID display
     * @param {string} meterId - Meter ID
     */
    updateMeterId(meterId) {
        if (this.elements.meterId) {
            this.elements.meterId.textContent = meterId || 'Initializing...';
        }
    }


    /**
     * Update reading display and animate gauge
     * @param {number} reading - Current reading in kWh
     */
    updateReading(reading) {
        if (this.elements.reading) {
            // Update text value
            this.elements.reading.textContent = reading.toFixed(3);

            // Add update animation
            this.elements.reading.classList.add('updating');
            setTimeout(() => {
                this.elements.reading.classList.remove('updating');
            }, 300);

            // Update gauge progress
            this.updateGauge(reading);

            logger.debug(`UI updated: Reading = ${reading} kWh`);
        }
    }

    /**
     * Update the circular gauge animation
     * @param {number} reading - Current reading in kWh
     */
    updateGauge(reading) {
        if (!this.elements.gaugeProgress) return;

        // Calculate progress (0-100%)
        const percentage = Math.min((reading / this.maxReading) * 100, 100);

        // Calculate stroke offset
        const offset = this.gaugeCircumference - (percentage / 100) * this.gaugeCircumference;

        // Update the gauge
        this.elements.gaugeProgress.style.strokeDashoffset = offset;

        logger.debug(`Gauge updated: ${percentage.toFixed(1)}%`);
    }

    /**
     * Update bill display
     * @param {number} bill - Current bill amount
     */
    updateBill(bill) {
        if (this.elements.bill) {
            this.elements.bill.textContent = formatCurrency(bill);

            // Add update animation
            this.elements.bill.classList.add('updating');
            setTimeout(() => {
                this.elements.bill.classList.remove('updating');
            }, 300);
            
            logger.debug(`UI updated: Bill = £${bill.toFixed(2)}`);
        }
    }
    
    /**
     * Update connection status display
     * @param {boolean} isConnected - Connection status
     */
    updateConnectionStatus(isConnected) {
        if (!this.elements.status) return;

        const statusText = isConnected ? 'Connected' : 'Disconnected';
        const statusClass = isConnected ? 'connected' : 'disconnected';

        this.elements.status.className = `status-badge ${statusClass}`;
        this.elements.status.innerHTML = `
            <span class="status-dot"></span>
            ${statusText}
        `;

        logger.debug(`UI updated: Connection status = ${statusText}`);

        // Clear error on successful connection
        if (isConnected) {
            this.hideError();
        }
    }

    /**
     * Show error message
     * @param {string} message - Error message
     */
    showError(message) {
        if (!this.elements.error || !this.elements.errorText) return;

        this.elements.errorText.textContent = message;
        this.elements.error.classList.add('show');

        logger.debug('Error displayed in UI');
    }

    /**
     * Hide error message
     */
    hideError() {
        if (this.elements.error) {
            this.elements.error.classList.remove('show');
        }
    }

    /**
     * Show alert message
     * @param {string} message - Alert message
     */
    showAlert(message) {
        if (!this.elements.alert || !this.elements.alertText) return;

        this.elements.alertText.textContent = message;
        this.elements.alert.classList.add('show');

        // Auto-hide after 10 seconds
        setTimeout(() => {
            this.hideAlert();
        }, 10000);

        logger.debug('Alert displayed in UI');
    }

    /**
     * Hide alert message
     */
    hideAlert() {
        if (this.elements.alert) {
            this.elements.alert.classList.remove('show');
        }
    }

    /**
     * Cleanup on application close
     */
    cleanup() {
        logger.info('Cleaning up application...');

        // Stop meter service
        meterService.stop();

        // Disconnect from server
        websocketService.disconnect();

        logger.info('Cleanup complete');
    }
}

// Create application instance
const app = new Application();

// Initialize when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        app.init().catch(error => {
            console.error('Failed to initialize application:', error);
        });
    });
} else {
    // DOM already loaded
    app.init().catch(error => {
        console.error('Failed to initialize application:', error);
    });
}

// Cleanup on window close
window.addEventListener('beforeunload', () => {
    app.cleanup();
});

// Export for testing/debugging
export { app };