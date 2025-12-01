/**
 * Configuration file for Smart Meter Client
 * Centralized settings for server connection, authentication, and meter behavior
 */

export const CONFIG = {
    SERVER: {
        URL: 'ws://127.0.0.1:8080',
        RECONNECT_DELAY: 3000,
        MAX_RECONNECT_ATTEMPTS: 5
    },

    AUTH: {
        API_KEY: '2B798FB1-F4EA-426C-B8B9-19DD0A946A4F'
    },

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
        'South Wales'
    ],

    METER: {
        MIN_READING_INCREMENT: 0.1,
        MAX_READING_INCREMENT: 0.5,
        MIN_INTERVAL: 15000,
        MAX_INTERVAL: 60000,
        INITIAL_READING: 0
    },

    UI: {
        CURRENCY_SYMBOL: '£',
        READING_UNIT: 'kWh',
        DECIMAL_PLACES: 2
    }
};

export function generateMeterId() {
    return crypto.randomUUID();
}

export function generateClientName() {
    const randomId = Math.random().toString(36).substr(2, 6).toUpperCase();
    return `Smart Meter ${randomId}`;
}

export function generateClientAddress(region) {
    const streetNumber = Math.floor(Math.random() * 999) + 1;
    const streets = ['High Street', 'Main Road', 'Church Lane', 'Station Road', 'Park Avenue'];
    const street = streets[Math.floor(Math.random() * streets.length)];
    return `${streetNumber} ${street}, ${region}`;
}

export function getRandomRegion() {
    return CONFIG.REGIONS[Math.floor(Math.random() * CONFIG.REGIONS.length)];
}

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