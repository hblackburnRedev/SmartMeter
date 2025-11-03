/**
 * Meter Service for Smart Meter Client
 * Handles meter reading generation and scheduling
 */

import { createLogger } from '../utils/logger.js';
import {
    generateReadingIncrement,
    generateReadingInterval,
    formatMilliseconds,
    roundTo
} from '../utils/helpers.js';
import { storageService } from './storage.service.js';

const logger = createLogger('MeterService');

/**
 * Meter Service Class
 * Generates and manages meter readings
 */
class MeterService {
    constructor() {
        this.isRunning = false;
        this.nextReadingTimeout = null;
        this.readingCallback = null;

        logger.info('Meter service initialized');
    }

    /**
     * Start generating meter readings
     * @param {Function} callback - Callback function to send readings (receives reading value)
     * @param {number} initialDelay - Initial delay before first reading (ms)
     */
    start(callback, initialDelay = 2000) {
        if (this.isRunning) {
            logger.warn('Meter service already running');
            return;
        }

        if (typeof callback !== 'function') {
            logger.error('Invalid callback provided to start()');
            throw new Error('Callback must be a function');
        }

        this.isRunning = true;
        this.readingCallback = callback;

        logger.info(`Starting meter service with initial delay of ${formatMilliseconds(initialDelay)}`);

        // Schedule first reading
        this.scheduleNextReading(initialDelay);
    }

    /**
     * Stop generating meter readings
     */
    stop() {
        if (!this.isRunning) {
            logger.warn('Meter service not running');
            return;
        }

        this.isRunning = false;

        // Clear scheduled timeout
        if (this.nextReadingTimeout) {
            clearTimeout(this.nextReadingTimeout);
            this.nextReadingTimeout = null;
        }

        logger.info('Meter service stopped');
    }

    /**
     * Check if meter service is running
     * @returns {boolean} Running status
     */
    isActive() {
        return this.isRunning;
    }

    /**
     * Generate a new meter reading
     * @returns {number} New cumulative reading
     */
    generateReading() {
        // Get current reading from storage
        const currentReading = storageService.getCurrentReading();

        // Generate random increment (0.1 - 0.5 kWh)
        const increment = generateReadingIncrement();

        // Calculate new cumulative reading
        const newReading = roundTo(currentReading + increment, 3);

        logger.debug(`Generated reading: ${newReading} kWh (increment: ${increment} kWh)`);

        return newReading;
    }

    /**
     * Process and send a meter reading
     */
    async processReading() {
        if (!this.isRunning) {
            logger.debug('Skipping reading - service not running');
            return;
        }

        try {
            // Generate new reading
            const newReading = this.generateReading();

            // Update storage
            storageService.updateReading(newReading);

            // Call callback to send reading (e.g., via WebSocket)
            if (this.readingCallback) {
                await this.readingCallback(newReading);
            }

            logger.info(`Reading processed: ${newReading} kWh`);

        } catch (error) {
            logger.error('Error processing reading', error);
            storageService.setError(`Failed to process reading: ${error.message}`);
        }

        // Schedule next reading
        this.scheduleNextReading();
    }

    /**
     * Schedule the next reading
     * @param {number} delay - Optional custom delay in milliseconds
     */
    scheduleNextReading(delay = null) {
        if (!this.isRunning) {
            logger.debug('Not scheduling next reading - service stopped');
            return;
        }

        // Generate random interval if not provided
        const interval = delay !== null ? delay : generateReadingInterval();

        logger.debug(`Next reading scheduled in ${formatMilliseconds(interval)}`);

        // Clear any existing timeout
        if (this.nextReadingTimeout) {
            clearTimeout(this.nextReadingTimeout);
        }

        // Schedule next reading
        this.nextReadingTimeout = setTimeout(() => {
            this.processReading();
        }, interval);
    }

    /**
     * Force an immediate reading (useful for testing)
     */
    forceReading() {
        logger.info('Forcing immediate reading');

        // Clear scheduled reading
        if (this.nextReadingTimeout) {
            clearTimeout(this.nextReadingTimeout);
            this.nextReadingTimeout = null;
        }

        // Process reading immediately
        this.processReading();
    }

    /**
     * Get next reading time estimate
     * @returns {string|null} Estimated time until next reading
     */
    getNextReadingInfo() {
        if (!this.isRunning || !this.nextReadingTimeout) {
            return null;
        }

        // Note: Can't get exact time from timeout, would need to track separately
        return 'Scheduled';
    }

    /**
     * Reset meter readings to initial state
     */
    reset() {
        logger.info('Resetting meter service');

        this.stop();

        // Reset storage reading to initial value
        storageService.updateReading(0);

        logger.info('Meter service reset complete');
    }
}

// Export singleton instance
export const meterService = new MeterService();