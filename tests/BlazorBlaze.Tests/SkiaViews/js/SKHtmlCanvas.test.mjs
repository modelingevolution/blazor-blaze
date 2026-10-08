import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(
    new URL('../../../../extern/SkiaSharp.Views.Blazor.Patched/wwwroot/SKHtmlCanvas.js', import.meta.url), 'utf8');
const { SKHtmlCanvas } = await import('data:text/javascript,' + encodeURIComponent(source));

const PixelOffset = 4;

globalThis.ImageData = class {
    constructor(data, width, height) {
        if (typeof SharedArrayBuffer !== 'undefined' && data.buffer instanceof SharedArrayBuffer)
            throw new TypeError('ImageData rejects SharedArrayBuffer-backed data');
        this.data = data;
        this.width = width;
        this.height = height;
    }
};

function rasterCanvas(heap) {
    globalThis.SkiaSharpModule = { HEAPU8: heap };
    const drawn = [];
    const element = { width: 0, height: 0, getContext: () => ({ putImageData: image => drawn.push(image) }) };
    return { view: new SKHtmlCanvas(false, element, () => { }), drawn };
}

function withoutSharedArrayBuffer(body) {
    const original = globalThis.SharedArrayBuffer;
    delete globalThis.SharedArrayBuffer;
    try {
        return body();
    } finally {
        globalThis.SharedArrayBuffer = original;
    }
}

test('putImageData draws when SharedArrayBuffer is undefined (page not cross-origin isolated)', () => {
    const { view, drawn } = rasterCanvas(new Uint8Array(new ArrayBuffer(PixelOffset + 2 * 2 * 4)));

    const result = withoutSharedArrayBuffer(() => view.putImageData(PixelOffset, 2, 2));

    assert.equal(result, true);
    assert.equal(drawn.length, 1);
});

test('putImageData copies the pixels out of a SharedArrayBuffer-backed heap', () => {
    const { view, drawn } = rasterCanvas(new Uint8Array(new SharedArrayBuffer(PixelOffset + 2 * 2 * 4)));

    const result = view.putImageData(PixelOffset, 2, 2);

    assert.equal(result, true);
    assert.equal(drawn.length, 1);
    assert.ok(!(drawn[0].data.buffer instanceof SharedArrayBuffer));
});

function animationFrames() {
    const frames = [];
    globalThis.window = {
        requestAnimationFrame: callback => frames.push(callback),
        cancelAnimationFrame: () => { }
    };
    return frames;
}

test('a one-shot frame whose callback fails does not block the next requested frame', async () => {
    const frames = animationFrames();
    let calls = 0;
    const view = new SKHtmlCanvas(false, { width: 0, height: 0 }, {
        invokeMethodAsync: async () => {
            calls++;
            if (calls === 1)
                throw new Error('frame failed');
        }
    });

    view.requestAnimationFrame(false, 0, 0);
    await assert.rejects(frames.shift()(), /frame failed/);
    view.requestAnimationFrame(false, 0, 0);
    await frames.shift()();

    assert.equal(calls, 2);
});

test('the render loop queues the next frame after a frame fails', async () => {
    const frames = animationFrames();
    let calls = 0;
    const view = new SKHtmlCanvas(false, { width: 0, height: 0 }, {
        invokeMethodAsync: async () => {
            calls++;
            if (calls === 1)
                throw new Error('frame failed');
        }
    });

    view.requestAnimationFrame(true, 0, 0);
    await assert.rejects(frames.shift()(), /frame failed/);

    assert.equal(frames.length, 1);
    await frames.shift()();
    assert.equal(calls, 2);
});

test('a resize requested while a frame is in flight is applied and drawn after that frame', async () => {
    const frames = animationFrames();
    const element = { width: 0, height: 0 };
    let finishFrame;
    const view = new SKHtmlCanvas(false, element, {
        invokeMethodAsync: () => new Promise(resolve => finishFrame = resolve)
    });

    view.requestAnimationFrame(false, 10, 10);
    const inFlight = frames.shift()();
    view.requestAnimationFrame(false, 20, 20);
    assert.equal(element.width, 10, 'the frame in flight keeps its canvas until it is put');
    element.width = 10;
    finishFrame();
    await inFlight;

    assert.equal(element.width, 20);
    assert.equal(element.height, 20);
    assert.equal(frames.length, 1);
});
