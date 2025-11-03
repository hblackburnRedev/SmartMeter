/**
 * Logging utility for Smart Meter Client
 * Provides consistent logging with different levels and timestamps
 */

// Log levels
const LogLevel = {
    DEBUG: 'DEBUG',
    INFO: 'INFO',
    WARN: 'WARN',
    ERROR: 'ERROR'
};

// ANSI color codes for console output
const Colors = {
    DEBUG: '\x1b[36m',   // Cyan
    INFO: '\x1b[32m',    // Green
    WARN: '\x1b[33m',    // Yellow
    ERROR: '\x1b[31m',   // Red
    RESET: '\x1b[0m'
};

/**
 * Format timestamp for log entries
 * @returns {string} Formatted timestamp
 */
function getTimestamp() {
    const now = new Date();
    return now.toISOString().replace('T', ' ').substring(0, 19);
}

/**
 * Core logging function
 * @param {string} level - Log level
 * @param {string} message - Log message
 * @param {*} data - Optional data to log
 */
function log(level, message, data = null) {
    const timestamp = getTimestamp();
    const color = Colors[level] || Colors.RESET;
    const prefix = `${color}[${timestamp}] [${level}]${Colors.RESET}`;

    if (data !== null && data !== undefined) {
        console.log(`${prefix} ${message}`, data);
    } else {
        console.log(`${prefix} ${message}`);
    }
}

/**
 * Logger class with chainable methods
 */
class Logger {
    constructor(context = '') {
        this.context = context;
    }

    /**
     * Log debug information (detailed diagnostic info)
     * @param {string} message - Debug message
     * @param {*} data - Optional data
     */
    debug(message, data = null) {
        const fullMessage = this.context ? `[${this.context}] ${message}` : message;
        log(LogLevel.DEBUG, fullMessage, data);
    }

    /**
     * Log general information
     * @param {string} message - Info message
     * @param {*} data - Optional data
     */
    info(message, data = null) {
        const fullMessage = this.context ? `[${this.context}] ${message}` : message;
        log(LogLevel.INFO, fullMessage, data);
    }

    /**
     * Log warnings
     * @param {string} message - Warning message
     * @param {*} data - Optional data
     */
    warn(message, data = null) {
        const fullMessage = this.context ? `[${this.context}] ${message}` : message;
        log(LogLevel.WARN, fullMessage, data);
    }

    /**
     * Log errors
     * @param {string} message - Error message
     * @param {Error|*} error - Error object or data
     */
    error(message, error = null) {
        const fullMessage = this.context ? `[${this.context}] ${message}` : message;

        if (error instanceof Error) {
            log(LogLevel.ERROR, fullMessage, {
                message: error.message,
                stack: error.stack
            });
        } else {
            log(LogLevel.ERROR, fullMessage, error);
        }
    }

    /**
     * Create a child logger with additional context
     * @param {string} childContext - Additional context
     * @returns {Logger} New logger instance
     */
    child(childContext) {
        const newContext = this.context
            ? `${this.context}:${childContext}`
            : childContext;
        return new Logger(newContext);
    }
}

/**
 * Create a logger instance with optional context
 * @param {string} context - Context name (e.g., 'WebSocket', 'MeterService')
 * @returns {Logger} Logger instance
 */
export function createLogger(context = '') {
    return new Logger(context);
}

// Export default logger for general use
export const logger = new Logger();

// Export LogLevel for external use if needed
export { LogLevel };