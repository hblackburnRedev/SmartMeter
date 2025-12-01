import { CONFIG, generateMeterId, generateClientName, generateClientAddress, getRandomRegion, validateConfig } from '../../config/config.js';
import { createLogger } from '../utils/logger.js';
import { formatCurrency } from '../utils/helpers.js';
import { storageService } from '../services/storage.service.js';
import { meterService } from '../services/meter.service.js';
import { websocketService } from '../services/websocket.service.js';

const logger = createLogger('Renderer');

class Application {
    constructor() {
        this.meterId = null;
        this.region = null;
        this.clientName = null;
        this.clientAddress = null;
        this.initialized = false;
        this.sessionStartTime = null;
        this.sessionTimer = null;

        this.elements = {
            status: null,
            reading: null,
            bill: null,
            error: null,
            errorText: null,
            alert: null,
            alertText: null,
            meterId: null,
            region: null,
            clientName: null,
            readingsSent: null,
            sessionTime: null,
            lastUpdate: null
        };
    }

    async init() {
        try {
            logger.info('Initializing Smart Meter Client');

            validateConfig();

            this.meterId = generateMeterId();
            this.region = getRandomRegion();
            this.clientName = generateClientName();
            this.clientAddress = generateClientAddress(this.region);

            logger.info(`Meter ID: ${this.meterId}`);
            logger.info(`Region: ${this.region}`);
            logger.info(`Name: ${this.clientName}`);
            logger.info(`Address: ${this.clientAddress}`);

            this.initializeDOMElements();

            storageService.initialize(this.meterId, this.region);

            websocketService.setClientDetails(this.clientName, this.clientAddress);

            this.subscribeToEvents();

            this.updateUI();

            await this.connectToServer();

            this.startMeterService();

            this.startSessionTimer();

            this.initialized = true;
            logger.info('Application initialized successfully');

        } catch (error) {
            logger.error('Initialization failed', error);
            this.showError(`Initialization failed: ${error.message}`);
        }
    }

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
            region: document.getElementById('region'),
            clientName: document.getElementById('client-name'),
            readingsSent: document.getElementById('readings-sent'),
            sessionTime: document.getElementById('session-time'),
            lastUpdate: document.getElementById('last-update')
        };

        for (const [key, element] of Object.entries(this.elements)) {
            if (!element) {
                throw new Error(`DOM element not found: ${key}`);
            }
        }

        logger.debug('DOM elements initialized');
    }

    subscribeToEvents() {
        storageService.subscribe('reading', (reading) => {
            this.updateReading(reading);
            this.updateStats();
            this.updateLastUpdate();
        });

        storageService.subscribe('bill', (bill) => {
            this.updateBill(bill);
        });

        storageService.subscribe('connection', (isConnected) => {
            this.updateConnectionStatus(isConnected);
        });

        storageService.subscribe('error', (error) => {
            this.showError(error);
        });

        storageService.subscribe('alert', (alert) => {
            this.showAlert(alert);
        });

        logger.debug('Event subscriptions established');
    }

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

    startMeterService() {
        logger.info('Starting meter service...');

        meterService.start(async (reading) => {
            try {
                await websocketService.sendReading(reading);
            } catch (error) {
                logger.error('Failed to send reading', error);
            }
        }, 2000);

        logger.info('Meter service started');
    }

    startSessionTimer() {
        this.sessionStartTime = Date.now();

        this.sessionTimer = setInterval(() => {
            this.updateSessionTime();
        }, 1000);
    }

    updateUI() {
        const state = storageService.getState();

        this.updateMeterId(state.meterId);
        this.updateRegion(this.region);
        this.updateClientName(this.clientName);
        this.updateReading(state.currentReading);
        this.updateBill(state.currentBill);
        this.updateConnectionStatus(state.isConnected);
        this.updateStats();
    }

    updateMeterId(meterId) {
        if (this.elements.meterId) {
            this.elements.meterId.textContent = meterId || 'Initializing...';
        }
    }

    updateRegion(region) {
        if (this.elements.region) {
            this.elements.region.textContent = region || '-';
        }
    }

    updateClientName(name) {
        if (this.elements.clientName) {
            this.elements.clientName.textContent = name || '-';
        }
    }

    updateReading(reading) {
        if (this.elements.reading) {
            this.elements.reading.textContent = reading.toFixed(3);

            this.elements.reading.classList.add('updating');
            setTimeout(() => {
                this.elements.reading.classList.remove('updating');
            }, 300);

            logger.debug(`UI updated: Reading = ${reading} kWh`);
        }
    }

    updateBill(bill) {
        if (this.elements.bill) {
            this.elements.bill.textContent = formatCurrency(bill);

            this.elements.bill.classList.add('updating');
            setTimeout(() => {
                this.elements.bill.classList.remove('updating');
            }, 300);

            logger.debug(`UI updated: Bill = £${bill.toFixed(2)}`);
        }
    }

    updateStats() {
        const stats = storageService.getStatistics();

        if (this.elements.readingsSent) {
            this.elements.readingsSent.textContent = stats.totalReadingsSent;
        }
    }

    updateSessionTime() {
        if (!this.sessionStartTime || !this.elements.sessionTime) return;

        const elapsed = Math.floor((Date.now() - this.sessionStartTime) / 1000);
        const minutes = Math.floor(elapsed / 60);
        const seconds = elapsed % 60;

        this.elements.sessionTime.textContent = `${minutes}:${seconds.toString().padStart(2, '0')}`;
    }

    updateLastUpdate() {
        if (this.elements.lastUpdate) {
            const now = new Date();
            const timeString = now.toLocaleTimeString('en-GB', {
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit'
            });
            this.elements.lastUpdate.textContent = timeString;
        }
    }

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

        if (isConnected) {
            this.hideError();
        }
    }

    showError(message) {
        if (!this.elements.error || !this.elements.errorText) return;

        this.elements.errorText.textContent = message;
        this.elements.error.classList.add('show');

        logger.debug('Error displayed in UI');
    }

    hideError() {
        if (this.elements.error) {
            this.elements.error.classList.remove('show');
        }
    }

    showAlert(message) {
        if (!this.elements.alert || !this.elements.alertText) return;

        this.elements.alertText.textContent = message;
        this.elements.alert.classList.add('show');

        setTimeout(() => {
            this.hideAlert();
        }, 10000);

        logger.debug('Alert displayed in UI');
    }

    hideAlert() {
        if (this.elements.alert) {
            this.elements.alert.classList.remove('show');
        }
    }

    cleanup() {
        logger.info('Cleaning up application...');

        if (this.sessionTimer) {
            clearInterval(this.sessionTimer);
        }

        meterService.stop();

        websocketService.disconnect();

        logger.info('Cleanup complete');
    }
}

const app = new Application();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        app.init().catch(error => {
            console.error('Failed to initialize application:', error);
        });
    });
} else {
    app.init().catch(error => {
        console.error('Failed to initialize application:', error);
    });
}

window.addEventListener('beforeunload', () => {
    app.cleanup();
});

export { app };