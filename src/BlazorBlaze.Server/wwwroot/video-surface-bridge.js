// ES module — single chokepoint for non-EA WebKitGTK messaging.
// Served at _content/BlazorBlaze.Server/video-surface-bridge.js

export function send(envelope) {
    if (window.webkit?.messageHandlers?.native) {
        window.webkit.messageHandlers.native.postMessage(JSON.stringify(envelope));
    }
}
