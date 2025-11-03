/**
 * Helper utility functions for Smart Meter Client
 * Formatting, validation, and common operations
 */

import { CONFIG } from '../../config/config.js';

/**
 * Format a number as currency (£)
 * @param {number} amount - Amount to format
 * @param {number} decimals - Number of decimal places (default: 2)
 * @returns {string} Formatted currency string
 */
export function formatCurrency(amount, decimals = CONFIG.UI.DECIMAL_PLACES) {
    if (typeof amount !== 'number' || isNaN(amount)) {
        return `${CONFIG.UI.CURRENCY_SYMBOL}0.00`;
    }
    return `${CONFIG.UI.CURRENCY_SYMBOL}${amount.toFixed(decimals)}`;
}

/**
 * Format a reading value with unit (kWh)
 * @param {number} reading - Reading value
 * @param {number} decimals - Number of decimal places (default: 3)
 * @returns {string} Formatted reading string
 */
export function formatReading(reading, decimals = 3) {
    if (typeof reading !== 'number' || isNaN(reading)) {
        return `0.000 ${CONFIG.UI.READING_UNIT}`;
    }
    return `${reading.toFixed(decimals)} ${CONFIG.UI.READING_UNIT}`;
}

/**
 * Format a timestamp for display
 * @param {Date|string|number} timestamp - Timestamp to format
 * @returns {string} Formatted timestamp
 */
export function formatTimestamp(timestamp) {
    const date = new Date(timestamp);
    if (isNaN(date.getTime())) {
        return 'Invalid Date';
    }

    const options = {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false
    };

    return date.toLocaleString('en-GB', options);
}

/**
 * Generate a random number between min and max
 * @param {number} min - Minimum value
 * @param {number} max - Maximum value
 * @returns {number} Random number
 */
export function randomBetween(min, max) {
    return Math.random() * (max - min) + min;
}

/**
 * Generate a random integer between min and max (inclusive)
 * @param {number} min - Minimum value
 * @param {number} max - Maximum value
 * @returns {number} Random integer
 */
export function randomIntBetween(min, max) {
    return Math.floor(Math.random() * (max - min + 1)) + min;
}

/**
 * Round a number to specified decimal places
 * @param {number} value - Value to round
 * @param {number} decimals - Number of decimal places
 * @returns {number} Rounded value
 */
export function roundTo(value, decimals = 2) {
    const multiplier = Math.pow(10, decimals);
    return Math.round(value * multiplier) / multiplier;
}

/**
 * Clamp a value between min and max
 * @param {number} value - Value to clamp
 * @param {number} min - Minimum value
 * @param {number} max - Maximum value
 * @returns {number} Clamped value
 */
export function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

/**
 * Sleep/delay for specified milliseconds
 * @param {number} ms - Milliseconds to sleep
 * @returns {Promise<void>} Promise that resolves after delay
 */
export function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

/**
 * Check if a value is a valid number
 * @param {*} value - Value to check
 * @returns {boolean} True if valid number
 */
export function isValidNumber(value) {
    return typeof value === 'number' && !isNaN(value) && isFinite(value);
}

/**
 * Parse a decimal string to number safely
 * @param {string} str - String to parse
 * @param {number} defaultValue - Default value if parsing fails
 * @returns {number} Parsed number or default
 */
export function parseDecimal(str, defaultValue = 0) {
    const parsed = parseFloat(str);
    return isValidNumber(parsed) ? parsed : defaultValue;
}

/**
 * Generate a random meter reading increment
 * @returns {number} Random increment in kWh
 */
export function generateReadingIncrement() {
    const increment = randomBetween(
        CONFIG.METER.MIN_READING_INCREMENT,
        CONFIG.METER.MAX_READING_INCREMENT
    );
    return roundTo(increment, 3);
}

/**
 * Generate a random interval for next reading
 * @returns {number} Random interval in milliseconds
 */
export function generateReadingInterval() {
    return randomIntBetween(
        CONFIG.METER.MIN_INTERVAL,
        CONFIG.METER.MAX_INTERVAL
    );
}

/**
 * Convert milliseconds to human-readable format
 * @param {number} ms - Milliseconds
 * @returns {string} Human-readable string (e.g., "45.5s", "1.2m")
 */
export function formatMilliseconds(ms) {
    if (ms < 1000) {
        return `${ms}ms`;
    } else if (ms < 60000) {
        return `${(ms / 1000).toFixed(1)}s`;
    } else {
        return `${(ms / 60000).toFixed(1)}m`;
    }
}

/**
 * Truncate a string to specified length with ellipsis
 * @param {string} str - String to truncate
 * @param {number} maxLength - Maximum length
 * @returns {string} Truncated string
 */
export function truncate(str, maxLength = 50) {
    if (!str || str.length <= maxLength) {
        return str;
    }
    return str.substring(0, maxLength - 3) + '...';
}

/**
 * Deep clone an object (simple implementation)
 * @param {*} obj - Object to clone
 * @returns {*} Cloned object
 */
export function deepClone(obj) {
    if (obj === null || typeof obj !== 'object') {
        return obj;
    }
    return JSON.parse(JSON.stringify(obj));
}