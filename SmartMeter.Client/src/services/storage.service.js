/**
 * Storage Service for Smart Meter Client
 * Manages application state in memory (readings, bill, connection status)
 */

import { createLogger } from '../utils/logger.js';
import { CONFIG } from '../../config/config.js';

const logger = createLogger('StorageService');

/**
 * Storage Service Class
 * Manages all client state with event notifications
 */
class StorageService {
    constructor() {
        this.state = {
            meterId: null,
            region: CONFIG.METER.DEFAULT_REGION,

            currentReading: CONFIG.METER.INITIAL_READING,
            lastReading: CONFIG.METER.INITIAL_READING,
            readingHistory: [],

            currentBill: 0,
            lastBill: 0,

            isConnected: false,
            connectionAttempts: 0,
            lastConnectionTime: null,

            lastError: null,
            lastAlert: null,

            totalReadingsSent: 0,
            sessionStartTime: new Date()
        };

        this.listeners = {
            reading: [],
            bill: [],
            connection: [],
            error: [],
            alert: []
        };

        logger.info('Storage service initialized');
    }

    /**
     * Set meter ID and region
     * @param {string} meterId - Unique meter identifier
     * @param {string} region - UK region
     */
    initialize(meterId, region = CONFIG.METER.DEFAULT_REGION) {
        this.state.meterId = meterId;
        this.state.region = region;
        this.state.sessionStartTime = new Date();
        logger.info(`Initialized with ID: ${meterId}, Region: ${region}`);
    }

    /**
     * Get current state
     * @returns {Object} Current state object
     */
    getState() {
        return { ...this.state };
    }

    /**
     * Get meter ID
     * @returns {string} Meter ID
     */
    getMeterId() {
        return this.state.meterId;
    }

    /**
     * Get region
     * @returns {string} Region
     */
    getRegion() {
        return this.state.region;
    }

    /**
     * Get current reading
     * @returns {number} Current reading in kWh
     */
    getCurrentReading() {
        return this.state.currentReading;
    }

    /**
     * Get current bill
     * @returns {number} Current bill in £
     */
    getCurrentBill() {
        return this.state.currentBill;
    }

    /**
     * Get connection status
     * @returns {boolean} Connection status
     */
    isConnected() {
        return this.state.isConnected;
    }

    /**
     * Update current reading
     * @param {number} reading - New reading value
     */
    updateReading(reading) {
        this.state.lastReading = this.state.currentReading;
        this.state.currentReading = reading;

        this.state.readingHistory.push({
            reading,
            timestamp: new Date(),
            bill: this.state.currentBill
        });

        if (this.state.readingHistory.length > 100) {
            this.state.readingHistory.shift();
        }

        this.state.totalReadingsSent++;

        logger.debug(`Reading updated: ${reading} kWh`);
        this.notifyListeners('reading', reading);
    }

    /**
     * Update current bill
     * @param {number} bill - New bill amount
     */
    updateBill(bill) {
        this.state.lastBill = this.state.currentBill;
        this.state.currentBill = bill;

        logger.debug(`Bill updated: £${bill.toFixed(2)}`);
        this.notifyListeners('bill', bill);
    }

    /**
     * Set connection status
     * @param {boolean} connected - Connection status
     */
    setConnectionStatus(connected) {
        const wasConnected = this.state.isConnected;
        this.state.isConnected = connected;

        if (connected) {
            this.state.lastConnectionTime = new Date();
            this.state.connectionAttempts = 0;
            logger.info('Connection status: Connected');
        } else {
            logger.warn('Connection status: Disconnected');
        }

        if (wasConnected !== connected) {
            this.notifyListeners('connection', connected);
        }
    }

    /**
     * Increment connection attempt counter
     */
    incrementConnectionAttempts() {
        this.state.connectionAttempts++;
        logger.debug(`Connection attempt: ${this.state.connectionAttempts}`);
    }

    /**
     * Get connection attempts count
     * @returns {number} Number of connection attempts
     */
    getConnectionAttempts() {
        return this.state.connectionAttempts;
    }

    /**
     * Set error message
     * @param {string} error - Error message
     */
    setError(error) {
        this.state.lastError = {
            message: error,
            timestamp: new Date()
        };

        logger.error('Error stored', error);
        this.notifyListeners('error', error);
    }

    /**
     * Clear error
     */
    clearError() {
        this.state.lastError = null;
        logger.debug('Error cleared');
    }

    /**
     * Set alert message
     * @param {string} alert - Alert message
     */
    setAlert(alert) {
        this.state.lastAlert = {
            message: alert,
            timestamp: new Date()
        };

        logger.warn('Alert stored', alert);
        this.notifyListeners('alert', alert);
    }

    /**
     * Get reading history
     * @param {number} limit - Maximum number of records to return
     * @returns {Array} Reading history
     */
    getReadingHistory(limit = 100) {
        return this.state.readingHistory.slice(-limit);
    }

    /**
     * Get session statistics
     * @returns {Object} Session statistics
     */
    getStatistics() {
        const now = new Date();
        const sessionDuration = now - this.state.sessionStartTime;

        return {
            totalReadingsSent: this.state.totalReadingsSent,
            sessionDuration: sessionDuration,
            sessionStartTime: this.state.sessionStartTime,
            averageReading: this.state.readingHistory.length > 0
                ? this.state.readingHistory.reduce((sum, r) => sum + r.reading, 0) / this.state.readingHistory.length
                : 0
        };
    }

    /**
     * Subscribe to state changes
     * @param {string} event - Event type (reading, bill, connection, error, alert)
     * @param {Function} callback - Callback function
     */
    subscribe(event, callback) {
        if (this.listeners[event]) {
            this.listeners[event].push(callback);
            logger.debug(`Subscribed to ${event} events`);
        } else {
            logger.warn(`Unknown event type: ${event}`);
        }
    }

    /**
     * Unsubscribe from state changes
     * @param {string} event - Event type
     * @param {Function} callback - Callback function to remove
     */
    unsubscribe(event, callback) {
        if (this.listeners[event]) {
            const index = this.listeners[event].indexOf(callback);
            if (index > -1) {
                this.listeners[event].splice(index, 1);
                logger.debug(`Unsubscribed from ${event} events`);
            }
        }
    }

    /**
     * Notify all listeners of an event
     * @param {string} event - Event type
     * @param {*} data - Event data
     */
    notifyListeners(event, data) {
        if (this.listeners[event]) {
            this.listeners[event].forEach(callback => {
                try {
                    callback(data);
                } catch (error) {
                    logger.error(`Error in ${event} listener`, error);
                }
            });
        }
    }

    /**
     * Reset all state (useful for testing)
     */
    reset() {
        this.state = {
            meterId: this.state.meterId,
            region: this.state.region,
            currentReading: CONFIG.METER.INITIAL_READING,
            lastReading: CONFIG.METER.INITIAL_READING,
            readingHistory: [],
            currentBill: 0,
            lastBill: 0,
            isConnected: false,
            connectionAttempts: 0,
            lastConnectionTime: null,
            lastError: null,
            lastAlert: null,
            totalReadingsSent: 0,
            sessionStartTime: new Date()
        };

        logger.info('State reset');
    }
}

export const storageService = new StorageService();