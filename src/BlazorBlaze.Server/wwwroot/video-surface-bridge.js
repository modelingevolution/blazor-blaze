// ES module — sole chokepoint for non-EA WebKitGTK postMessage traffic.
// Served at _content/BlazorBlaze.Server/video-surface-bridge.js
//
// Carries overlay canvas, MJPEG routing, and the one-shot bootstrap GUID.
// EventAggregator events must NOT flow through here — they travel over the
// dedicated WebSocket transport.

/**
 * Posts an envelope to the native WebKitGTK message handler.
 * Silent no-op when running outside WebKitGTK (Chromium, Firefox, etc.).
 * This is the ONLY call site permitted to invoke
 * window.webkit.messageHandlers.native.postMessage.
 * @param {object} envelope
 */
export function send(envelope) {
    const handler = window.webkit?.messageHandlers?.native;
    if (!handler) return;
    try {
        handler.postMessage(JSON.stringify(envelope));
    } catch (e) {
        if (handler) console.warn('[bridge] postMessage failed:', e);
    }
}

/**
 * One-shot bootstrap: delivers the EventAggregator session GUID to the
 * native host. Called once per Blazor circuit start.
 * @param {string} guid
 */
export function sendSessionBootstrap(guid) {
    if (!guid) { console.warn('[bridge] sendSessionBootstrap called with empty guid'); return; }
    send({ type: "event-aggregator-session", id: guid });
}
