// BlazorBlaze JavaScript interop functions

window.getDevicePixelRatio = () => {
    return window.devicePixelRatio;
};

window.getElementBoundingClientRect = (element) => {
    const rect = element.getBoundingClientRect();

    return {
        width: element.clientWidth,
        height: element.clientHeight,
        offsetWidth: element.offsetWidth,
        offsetHeight: element.offsetHeight,
        scrollWidth: element.scrollWidth,
        scrollHeight: element.scrollHeight,
        boundingClientRect: {
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height,
            top: rect.top,
            right: rect.right,
            bottom: rect.bottom,
            left: rect.left
        }
    };
};
