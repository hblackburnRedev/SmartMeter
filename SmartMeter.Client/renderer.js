// Configuration
const SERVER_URL = 'ws://127.0.0.1:8080'; // WebSocket URL
const METER_ID = `METER_${Math.random().toString(36).substr(2, 9).toUpperCase()}`;

let ws = null;
let currentReading = 0;
let currentBill = 0;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 5;
const RECONNECT_DELAY = 3000;

// DOM Elements
const statusEl = document.getElementById('status');
const readingEl = document.getElementById('reading');
const billEl = document.getElementById('bill');
const errorEl = document.getElementById('error');
const errorText = document.getElementById('error-text');
const alertEl = document.getElementById('alert');
const alertText = document.getElementById('alert-text');
const meterIdEl = document.getElementById('meter-id');

// Initialize
meterIdEl.textContent = METER_ID;

// WebSocket Connection
function connectToServer() {
    try {
        updateStatus(false, 'Connecting...');

        ws = new WebSocket(SERVER_URL);

        ws.onopen = () => {
            console.log('✓ Connected to server');
            reconnectAttempts = 0;
            updateStatus(true, 'Connected');
            hideError();

            // Send authentication/identification message
            sendMessage({
                type: 'auth',
                meterId: METER_ID,
                timestamp: new Date().toISOString()
            });

            // Start sending readings after connection
            setTimeout(generateReading, 2000);
        };

        ws.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data);
                console.log('← Received:', data);
                handleServerMessage(data);
            } catch (error) {
                console.log('← Received (raw):', event.data);
            }
        };

        ws.onerror = (error) => {
            console.error('WebSocket error:', error);
            showError('Connection error occurred');
        };

        ws.onclose = (event) => {
            console.log('✗ Disconnected from server');
            updateStatus(false, 'Disconnected');

            if (event.code !== 1000) { // Not a normal closure
                showError('Connection lost. Reconnecting...');
                attemptReconnect();
            }
        };

    } catch (error) {
        console.error('Failed to connect:', error);
        showError('Failed to connect to server');
        attemptReconnect();
    }
}

function attemptReconnect() {
    if (reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
        reconnectAttempts++;
        console.log(`Reconnect attempt ${reconnectAttempts}/${MAX_RECONNECT_ATTEMPTS} in ${RECONNECT_DELAY/1000}s...`);
        setTimeout(connectToServer, RECONNECT_DELAY);
    } else {
        showError('Unable to connect to server. Maximum reconnection attempts reached.');
    }
}

function sendMessage(data) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        const message = JSON.stringify(data);
        ws.send(message);
        console.log('→ Sent:', data);
        return true;
    } else {
        console.error('WebSocket not connected');
        showError('Cannot send - not connected to server');
        return false;
    }
}

function handleServerMessage(data) {
    switch (data.type) {
        case 'bill_update':
            currentBill = data.bill || data.amount || 0;
            updateDisplay();
            break;

        case 'grid_alert':
            showAlert(data.message || 'Grid alert received');
            break;

        case 'error':
            showError(data.message || 'Server error occurred');
            break;

        case 'auth_success':
            console.log('✓ Authentication successful');
            break;

        case 'reading_acknowledged':
            console.log('✓ Reading acknowledged by server');
            break;

        default:
            console.log('Unknown message type:', data.type);
    }
}

// Generate and send readings
function generateReading() {
    // Only generate if connected
    if (!ws || ws.readyState !== WebSocket.OPEN) {
        console.log('Skipping reading - not connected');
        scheduleNextReading();
        return;
    }

    // Generate realistic reading increment (between 0.1 and 0.5 kWh)
    const increment = (Math.random() * 0.4 + 0.1).toFixed(3);
    currentReading = (parseFloat(currentReading) + parseFloat(increment)).toFixed(3);

    const reading = {
        type: 'meter_reading',
        meterId: METER_ID,
        reading: parseFloat(currentReading),
        unit: 'kWh',
        timestamp: new Date().toISOString()
    };

    if (sendMessage(reading)) {
        updateDisplay();
    }

    scheduleNextReading();
}

function scheduleNextReading() {
    // Random interval between 15 and 60 seconds (15000ms to 60000ms)
    const interval = Math.floor(Math.random() * 45000) + 15000;
    console.log(`Next reading in ${(interval/1000).toFixed(1)}s`);
    setTimeout(generateReading, interval);
}

// UI Updates
function updateStatus(isConnected, text) {
    statusEl.className = `status-badge ${isConnected ? 'connected' : 'disconnected'}`;
    statusEl.innerHTML = `
        <span class="status-dot"></span>
        ${text}
    `;
}

function updateDisplay() {
    readingEl.textContent = `${currentReading} kWh`;
    billEl.textContent = `£${currentBill.toFixed(2)}`;
}

function showError(message) {
    errorText.textContent = message;
    errorEl.classList.add('show');
}

function hideError() {
    errorEl.classList.remove('show');
}

function showAlert(message) {
    alertText.textContent = message;
    alertEl.classList.add('show');
    setTimeout(() => {
        alertEl.classList.remove('show');
    }, 10000); // Hide after 10 seconds
}

// Cleanup on window close
window.addEventListener('beforeunload', () => {
    if (ws) {
        ws.close(1000, 'Client closing');
    }
});

// Start connection
connectToServer();