/**
 * Script to launch multiple Smart Meter clients
 * Usage: node run-multiple.js [number_of_clients]
 * Example: node run-multiple.js 12
 */

import { spawn } from 'child_process';
import path from 'path';
import { fileURLToPath } from 'url';

// Get __dirname equivalent in ES6 modules
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Get number of clients from command line argument (default: 12)
const numClients = parseInt(process.argv[2]) || 12;

// Validate input
if (numClients < 1 || numClients > 50) {
    console.error('Error: Number of clients must be between 1 and 50');
    process.exit(1);
}

console.log('===========================================');
console.log('  Smart Meter Client - Multi-Instance');
console.log('===========================================');
console.log(`Starting ${numClients} client(s)...\n`);

// Store child processes
const clients = [];

// Track startup progress
let startedCount = 0;

/**
 * Launch a single client instance
 * @param {number} index - Client index
 */
function launchClient(index) {
    return new Promise((resolve) => {
        const clientNumber = index + 1;

        // Spawn electron process
        const client = spawn('npm', ['start'], {
            shell: true,
            cwd: __dirname,
            stdio: 'inherit' // Inherit stdio to see all logs
        });

        clients.push(client);

        // Handle client exit
        client.on('exit', (code, signal) => {
            if (code !== 0 && code !== null) {
                console.log(`\nClient ${clientNumber} exited with code ${code}`);
            }
        });

        client.on('error', (error) => {
            console.error(`\nClient ${clientNumber} error:`, error.message);
        });

        // Log startup
        console.log(`[${clientNumber}/${numClients}] Client started`);
        startedCount++;

        // Small delay before resolving to stagger launches
        setTimeout(() => {
            resolve();
        }, 100);
    });
}

/**
 * Launch all clients sequentially with staggered timing
 */
async function launchAllClients() {
    for (let i = 0; i < numClients; i++) {
        await launchClient(i);

        // Stagger launches by 1 second each
        if (i < numClients - 1) {
            await new Promise(resolve => setTimeout(resolve, 1000));
        }
    }

    console.log('\n===========================================');
    console.log(`All ${numClients} clients started successfully!`);
    console.log('Press Ctrl+C to stop all clients');
    console.log('===========================================\n');
}

/**
 * Handle cleanup on exit
 */
function cleanup() {
    console.log('\n\n===========================================');
    console.log('Shutting down all clients...');
    console.log('===========================================\n');

    // Kill all client processes
    clients.forEach((client, index) => {
        try {
            if (!client.killed) {
                client.kill('SIGTERM');
                console.log(`Client ${index + 1} terminated`);
            }
        } catch (error) {
            console.error(`Error terminating client ${index + 1}:`, error.message);
        }
    });

    console.log('\nAll clients shut down. Goodbye!\n');
    process.exit(0);
}

// Handle Ctrl+C (SIGINT)
process.on('SIGINT', cleanup);

// Handle terminal close (SIGTERM)
process.on('SIGTERM', cleanup);

// Handle uncaught exceptions
process.on('uncaughtException', (error) => {
    console.error('\nUncaught Exception:', error);
    cleanup();
});

// Start launching clients
launchAllClients().catch((error) => {
    console.error('\nError launching clients:', error);
    cleanup();
});