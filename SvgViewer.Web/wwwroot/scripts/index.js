
// SVG Editor Module
window.svgEditor = (function() {
    let draw = null;
    let elementMap = new Map();
    let selectedElementId = null;

    function init() {
        const container = document.getElementById('svg-canvas');
        if (container) {
            container.innerHTML = '';
            draw = SVG().addTo('#svg-canvas');
        }
    }

    function renderCanvas(width, height, elements) {
        if (!draw) init();
        if (!draw) return;

        draw.clear();
        draw.size(width, height);
        elementMap.clear();

        // Render all elements
        elements.forEach(element => {
            renderElement(element);
        });
    }

    function renderElement(elementData) {
        if (!draw) return;

        let svgElement = null;

        // Create SVG element based on type
        switch (elementData.type) {
            case 'rect':
                svgElement = draw.rect(elementData.width, elementData.height)
                    .move(elementData.x, elementData.y);
                break;

            case 'circle':
                svgElement = draw.circle(elementData.radius * 2)
                    .center(elementData.x, elementData.y);
                break;

            case 'ellipse':
                svgElement = draw.ellipse(elementData.radiusX * 2, elementData.radiusY * 2)
                    .center(elementData.x, elementData.y);
                break;

            case 'line':
                svgElement = draw.line(elementData.x, elementData.y, 
                    elementData.x + elementData.width, elementData.y + elementData.height);
                break;

            case 'polyline':
                if (elementData.points) {
                    svgElement = draw.polyline(elementData.points);
                }
                break;

            case 'polygon':
                if (elementData.points) {
                    svgElement = draw.polygon(elementData.points);
                }
                break;

            case 'path':
                if (elementData.pathData) {
                    svgElement = draw.path(elementData.pathData);
                }
                break;

            case 'text':
                svgElement = draw.text(elementData.text || '')
                    .move(elementData.x, elementData.y);
                break;

            case 'g':
            case 'group':
                svgElement = draw.group();
                break;
        }

        if (svgElement) {
            // Apply common styling
            if (elementData.fill) {
                svgElement.fill(elementData.fill);
            }
            if (elementData.stroke) {
                svgElement.stroke({ 
                    color: elementData.stroke, 
                    width: elementData.strokeWidth || 1 
                });
            }
            if (elementData.opacity !== undefined && elementData.opacity !== 1) {
                svgElement.opacity(elementData.opacity);
            }
            if (elementData.transform) {
                svgElement.attr('transform', elementData.transform);
            }

            // Add custom logic attributes as data attributes
            Object.keys(elementData.logicAttributes || {}).forEach(key => {
                svgElement.attr(`data-logic-${key}`, elementData.logicAttributes[key]);
            });

            // Set ID and store reference
            svgElement.attr('id', elementData.id);
            elementMap.set(elementData.id, svgElement);

            // Add click handler
            svgElement.css('cursor', 'pointer');
            svgElement.on('click', function(e) {
                e.stopPropagation();
                highlightElement(elementData.id);
                // Notify Blazor about selection (would require DotNetObjectReference)
            });

            // Add hover effect
            svgElement.on('mouseenter', function() {
                if (selectedElementId !== elementData.id) {
                    this.attr({ 'stroke-width': (elementData.strokeWidth || 1) + 1 });
                }
            });

            svgElement.on('mouseleave', function() {
                if (selectedElementId !== elementData.id) {
                    this.attr({ 'stroke-width': elementData.strokeWidth || 1 });
                }
            });
        }
    }

    function highlightElement(elementId) {
        // Remove previous highlight
        if (selectedElementId && elementMap.has(selectedElementId)) {
            const prevElement = elementMap.get(selectedElementId);
            prevElement.attr({ 'stroke-dasharray': 'none' });
        }

        // Add new highlight
        selectedElementId = elementId;
        if (elementMap.has(elementId)) {
            const element = elementMap.get(elementId);
            element.attr({ 
                'stroke-dasharray': '5,5',
                'stroke': element.attr('stroke') || '#2196F3',
                'stroke-width': (parseFloat(element.attr('stroke-width')) || 1) + 2
            });
        }
    }

    function updateElement(elementData) {
        if (!elementMap.has(elementData.id)) return;

        const svgElement = elementMap.get(elementData.id);

        // Update based on type
        switch (elementData.type) {
            case 'rect':
                svgElement.size(elementData.width, elementData.height)
                    .move(elementData.x, elementData.y);
                break;
            case 'circle':
                svgElement.radius(elementData.radius)
                    .center(elementData.x, elementData.y);
                break;
            // Add other types as needed
        }

        // Update styling
        if (elementData.fill) svgElement.fill(elementData.fill);
        if (elementData.stroke) {
            svgElement.stroke({ 
                color: elementData.stroke, 
                width: elementData.strokeWidth || 1 
            });
        }
        svgElement.opacity(elementData.opacity || 1);

        // Update logic attributes
        Object.keys(elementData.logicAttributes || {}).forEach(key => {
            svgElement.attr(`data-logic-${key}`, elementData.logicAttributes[key]);
        });
    }

    function enableDragging(elementId) {
        if (!elementMap.has(elementId)) return;

        const element = elementMap.get(elementId);
        element.draggable();
    }

    function getElementBounds(elementId) {
        if (!elementMap.has(elementId)) return null;

        const element = elementMap.get(elementId);
        const bbox = element.bbox();
        return {
            x: bbox.x,
            y: bbox.y,
            width: bbox.width,
            height: bbox.height
        };
    }

    return {
        init,
        renderCanvas,
        renderElement,
        highlightElement,
        updateElement,
        enableDragging,
        getElementBounds
    };
})();

// Download helper
window.downloadSvg = function(filename, content) {
    const blob = new Blob([content], { type: 'image/svg+xml' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};