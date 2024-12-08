// resizeInterop.js
export function initializeResizeHandlers(dotNetHelper) {
    if (!window.resizeInterop) {
        window.resizeInterop = {
            dotNetHelper: null,
            startPosition: null,
            isMoving: false
        };
    }
    window.resizeInterop.dotNetHelper = dotNetHelper;
}

export function attachResizeListeners(dotNetHelper, startPosition) {
    // Ensure we have a resizeInterop object
    if (!window.resizeInterop) {
        window.resizeInterop = {};
    }

    // Store the dotNetHelper and start position
    window.resizeInterop.dotNetHelper = dotNetHelper;
    window.resizeInterop.startPosition = startPosition;
    window.resizeInterop.isMoving = startPosition.isMoving || false;

    // Remove any existing listeners to prevent multiple attachments
    removeResizeListeners();

    // Attach new listeners
    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
}

function handleMouseMove(e) {
    const resizeInterop = window.resizeInterop;
    if (resizeInterop && resizeInterop.dotNetHelper) {
        try {
            resizeInterop.dotNetHelper.invokeMethodAsync('HandleMouseMove', e.clientX, e.clientY);
        } catch (error) {
            console.error('Error in handleMouseMove:', error);
        }
    }
}

function handleMouseUp() {
    const resizeInterop = window.resizeInterop;
    if (resizeInterop && resizeInterop.dotNetHelper) {
        try {
            resizeInterop.dotNetHelper.invokeMethodAsync('HandleMouseUp');
            resizeInterop.dotNetHelper = null;
        } catch (error) {
            console.error('Error in handleMouseUp:', error);
        }
    }
    removeResizeListeners();
}

export function removeResizeListeners() {
    document.removeEventListener('mousemove', handleMouseMove);
    document.removeEventListener('mouseup', handleMouseUp);
}

export function focus(element) {
    if (element) {
        element.focus();
    }
}