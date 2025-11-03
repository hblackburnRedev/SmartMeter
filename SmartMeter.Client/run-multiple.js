/**
 * Script to launch multiple Smart Meter clients
 */

import { spawn } from 'child_process';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const numClients = parseInt(process.argv[2]) || 12;

if (numClients < 1 || numClients > 50) {
    console.error('Error: Number of clients must be between 1 and 50');
    process.exit(1);
}

console.log('===========================================');
console.log('  Smart Meter Client - Multi-Instance');
console.log('===========================================');
console.log(`Starting ${numClients} client(s)...\n`);

const clients = [];

let startedCount = 0;

/**
 * Launch a single client instance
 * @param {number} index - Client index
 */
function launchClient(index) {
    return new Promise((resolve) => {
        const clientNumber = index + 1;

        const client = spawn('npm', ['start'], {
            shell: true,
            cwd: __dirname,
            stdio: 'inherit'
        });

        clients.push(client);

        client.on('exit', (code, signal) => {
            if (code !== 0 && code !== null) {
                console.log(`\nClient ${clientNumber} exited with code ${code}`);
            }
        });

        client.on('error', (error) => {
            console.error(`\nClient ${clientNumber} error:`, error.message);
        });

        console.log(`[${clientNumber}/${numClients}] Client started`);
        startedCount++;

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

process.on('SIGINT', cleanup);

process.on('SIGTERM', cleanup);

process.on('uncaughtException', (error) => {
    console.error('\nUncaught Exception:', error);
    cleanup();
});

launchAllClients().catch((error) => {
    console.error('\nError launching clients:', error);
    cleanup();
});