/**
 * Configuration file for Smart Meter Client
 * Centralized settings for server connection, authentication, and meter behavior
 */

export const CONFIG = {
    // Server Connection Settings
    SERVER: {
        URL: 'ws://127.0.0.1:8080',
        RECONNECT_DELAY: 3000,           // 3 seconds
        MAX_RECONNECT_ATTEMPTS: 5
    },

    // Authentication
    AUTH: {
        API_KEY: '2B798FB1-F4EA-426C-B8B9-19DD0A946A4F',  // Must match server's ApiKey in appsettings.json
        CLIENT_ID_PREFIX: 'METER_'
    },

    // UK Electricity Regions (matching server CSV)
    REGIONS: [
        'North West',
        'Northern',
        'Yorkshire',
        'Northern Scotland',
        'Southern',
        'Southern Scotland',
        'North Wales and Mersey',
        'London',
        'South East',
        'Eastern',
        'East Midlands',
        'Midlands',
        'Southern Western',
        'South Wales',
        'Great Britain average'
    ],

    // Meter Settings
    METER: {
        DEFAULT_REGION: 'London',        // Default UK region
        MIN_READING_INCREMENT: 0.1,      // kWh
        MAX_READING_INCREMENT: 0.5,      // kWh
        MIN_INTERVAL: 15000,             // 15 seconds in milliseconds
        MAX_INTERVAL: 60000,             // 60 seconds in milliseconds
        INITIAL_READING: 0               // Starting kWh reading
    },

    // UI Settings
    UI: {
        CURRENCY_SYMBOL: '£',
        READING_UNIT: 'kWh',
        DECIMAL_PLACES: 2
    }
};

/**
 * Generate a unique client/meter ID
 * @returns {string} Unique meter identifier
 */
export function generateMeterId() {
    const randomId = Math.random().toString(36).substr(2, 9).toUpperCase();
    return `${CONFIG.AUTH.CLIENT_ID_PREFIX}${randomId}`;
}

/**
 * Get a random UK region from the available regions
 * @returns {string} Random region name
 */
export function getRandomRegion() {
    const regions = CONFIG.REGIONS.filter(r => r !== 'Great Britain average');
    return regions[Math.floor(Math.random() * regions.length)];
}

/**
 * Validate configuration on startup
 * @throws {Error} If configuration is invalid
 */
export function validateConfig() {
    if (!CONFIG.SERVER.URL) {
        throw new Error('Server URL is not configured');
    }

    if (!CONFIG.AUTH.API_KEY) {
        throw new Error('API Key is not configured');
    }

    if (CONFIG.REGIONS.length === 0) {
        throw new Error('No regions configured');
    }

    if (CONFIG.METER.MIN_INTERVAL >= CONFIG.METER.MAX_INTERVAL) {
        throw new Error('Invalid meter interval configuration');
    }

    console.log('✓ Configuration validated successfully');
}