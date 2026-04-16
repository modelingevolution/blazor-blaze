// ES module — no globals on window.
// Served at _content/BlazorBlaze.Server/video-surface.js

import { send as _bridgeSend } from './video-surface-bridge.js';

const _activeAdapters = new Set();

export function postNativeMessage(message) {
    _bridgeSend(message);
}

class NativePlayerAdapter {
    #element;
    #playerId;
    #resizeObserver;
    #mutationObserver;
    #rafPending = false;
    #lastRect = { x: 0, y: 0, width: 0, height: 0 };
    #backgroundColor = null;
    #destroyed = false;

    constructor(element, playerId) {
        this.#element = element;
        this.#playerId = playerId;
    }

    init(streamUrl) {
        const rect = this.#getRect();
        this.#lastRect = rect;

        postNativeMessage({
            type: "init",
            id: this.#playerId,
            position: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
            streamUrl: streamUrl
        });

        this.#startPositionTracking();

        // Delayed retries for layout settling.
        setTimeout(() => this.#checkPosition(), 100);
        setTimeout(() => this.#checkPosition(), 500);
    }

    play() {
        postNativeMessage({ type: "play", id: this.#playerId });
    }

    pause() {
        postNativeMessage({ type: "pause", id: this.#playerId });
    }

    resume() {
        postNativeMessage({ type: "resume", id: this.#playerId });
    }

    refresh() {
        postNativeMessage({ type: "refresh", id: this.#playerId });
    }

    destroy() {
        if (this.#destroyed) return;
        this.#destroyed = true;

        postNativeMessage({ type: "destroy-player", id: this.#playerId });
        this.#stopPositionTracking();
        _activeAdapters.delete(this);
    }

    postMessage(msg) {
        postNativeMessage(msg);
    }

    setBackgroundColor(color) {
        this.#backgroundColor = color;
        postNativeMessage({ type: "set-background-color", color: color });
    }

    getBackgroundColor() {
        return this.#backgroundColor;
    }

    // --- Position tracking ---

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
        window.addEventListener("scroll", this._onScroll, true); // capture phase for nested containers
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
        if (this.#rafPending || this.#destroyed) return;
        this.#rafPending = true;
        requestAnimationFrame(() => {
            this.#rafPending = false;
            this.#checkPosition();
        });
    }

    #checkPosition() {
        if (this.#destroyed) return;

        const rect = this.#getRect();
        if (rect.x === this.#lastRect.x &&
            rect.y === this.#lastRect.y &&
            rect.width === this.#lastRect.width &&
            rect.height === this.#lastRect.height) {
            return;
        }

        this.#lastRect = rect;
        postNativeMessage({
            type: "set-layout",
            id: this.#playerId,
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height
        });
    }

    #getRect() {
        const r = this.#element.getBoundingClientRect();
        return {
            x: Math.round(r.x),
            y: Math.round(r.y),
            width: Math.round(r.width),
            height: Math.round(r.height)
        };
    }
}

// Cleanup all adapters on beforeunload.
window.addEventListener("beforeunload", () => {
    for (const adapter of _activeAdapters) {
        adapter.destroy();
    }
});

/**
 * Creates a native player adapter for the given element.
 * @param {HTMLElement} element - The placeholder element.
 * @param {string} playerId - The unique player ID.
 * @returns {NativePlayerAdapter} The adapter instance.
 */
export function createAdapter(element, playerId) {
    const adapter = new NativePlayerAdapter(element, playerId);
    _activeAdapters.add(adapter);
    return adapter;
}
