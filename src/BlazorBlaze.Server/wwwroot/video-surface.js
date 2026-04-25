// ES module — no globals on window.
// Served at _content/BlazorBlaze.Server/video-surface.js
//
// All native-host traffic goes through video-surface-bridge.js — the single
// postMessage chokepoint (FR-10, NFR-5). Do NOT call
// window.webkit.messageHandlers.native.postMessage from this file.
//
// After the EventAggregator session GUID has been delivered (one-shot bootstrap
// via video-surface-bridge.js), every subsequent message from .NET to native
// flows through the EventAggregator WebSocket as a [ProtoContract] event.
// This adapter therefore performs ZERO native posts; it only watches DOM rect
// changes and forwards them back to .NET via the supplied DotNetObjectReference.

import { send as bridgeSend, sendSessionBootstrap } from "./video-surface-bridge.js";

const _activeAdapters = new Set();

/**
 * Posts a message to the native WebKitGTK host via the bridge.
 * Retained as a named export for low-level C# interop callers; adapter
 * instances must NOT use it.
 * @param {object} message - The message to send.
 */
export function postNativeMessage(message) {
    bridgeSend(message);
}

/**
 * One-shot bootstrap GUID delivery. Exposed to C# interop so the forwarder
 * can hand off the session GUID exactly once per circuit start.
 * @param {string} guid
 */
export function postSessionBootstrap(guid) {
    sendSessionBootstrap(guid);
}

class NativePlayerAdapter {
    #element;
    #playerId;
    #dotnetRef;
    #resizeObserver;
    #mutationObserver;
    #rafPending = false;
    #lastRect = { x: 0, y: 0, width: 0, height: 0 };
    #disposed = false;

    constructor(element, playerId, dotnetRef) {
        this.#element = element;
        this.#playerId = playerId;
        this.#dotnetRef = dotnetRef;
        this.#lastRect = this.#computeRect();
        this.#startPositionTracking();

        // Delayed retries for layout settling.
        setTimeout(() => this.#checkPosition(), 100);
        setTimeout(() => this.#checkPosition(), 500);
    }

    getRect() {
        return this.#lastRect;
    }

    dispose() {
        if (this.#disposed) return;
        this.#disposed = true;
        this.#stopPositionTracking();
        this.#dotnetRef = null;
        _activeAdapters.delete(this);
    }

    #startPositionTracking() {
        this.#resizeObserver = new ResizeObserver(() => this.#schedulePositionUpdate());
        this.#resizeObserver.observe(this.#element);

        this.#mutationObserver = new MutationObserver(() => this.#schedulePositionUpdate());
        this.#mutationObserver.observe(document.body, {
            subtree: true,
            attributes: true,
            attributeFilter: ["style", "class"]
        });

        this._onResize = () => this.#schedulePositionUpdate();
        this._onScroll = () => this.#schedulePositionUpdate();

        window.addEventListener("resize", this._onResize);
        window.addEventListener("scroll", this._onScroll, true);
    }

    #stopPositionTracking() {
        if (this.#resizeObserver) {
            this.#resizeObserver.disconnect();
            this.#resizeObserver = null;
        }
        if (this.#mutationObserver) {
            this.#mutationObserver.disconnect();
            this.#mutationObserver = null;
        }
        if (this._onResize) {
            window.removeEventListener("resize", this._onResize);
            this._onResize = null;
        }
        if (this._onScroll) {
            window.removeEventListener("scroll", this._onScroll, true);
            this._onScroll = null;
        }
    }

    #schedulePositionUpdate() {
        if (this.#rafPending || this.#disposed) return;
        this.#rafPending = true;
        requestAnimationFrame(() => {
            this.#rafPending = false;
            this.#checkPosition();
        });
    }

    #checkPosition() {
        if (this.#disposed) return;

        const rect = this.#computeRect();
        if (rect.x === this.#lastRect.x &&
            rect.y === this.#lastRect.y &&
            rect.width === this.#lastRect.width &&
            rect.height === this.#lastRect.height) {
            return;
        }

        this.#lastRect = rect;
        const ref = this.#dotnetRef;
        if (!ref) return;
        try {
            ref.invokeMethodAsync("OnRectChanged", rect.x, rect.y, rect.width, rect.height);
        } catch (e) {
            console.warn('[video-surface] OnRectChanged invocation failed:', e);
        }
    }

    #computeRect() {
        const r = this.#element.getBoundingClientRect();
        return {
            x: Math.round(r.x),
            y: Math.round(r.y),
            width: Math.round(r.width),
            height: Math.round(r.height)
        };
    }
}

window.addEventListener("beforeunload", () => {
    for (const adapter of _activeAdapters) {
        adapter.dispose();
    }
});

/**
 * Creates a native player adapter for the given element. The adapter only
 * watches rect changes; all native traffic flows through the EventAggregator.
 * @param {HTMLElement} element - The placeholder element.
 * @param {string} playerId - The unique player ID.
 * @param {object} dotnetRef - DotNetObjectReference to the owning .NET component (must expose OnRectChanged).
 * @returns {NativePlayerAdapter} The adapter instance.
 */
export function createAdapter(element, playerId, dotnetRef) {
    const adapter = new NativePlayerAdapter(element, playerId, dotnetRef);
    _activeAdapters.add(adapter);
    return adapter;
}
