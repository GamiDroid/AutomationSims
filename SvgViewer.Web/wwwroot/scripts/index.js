import { SVG } from '@svgdotjs/svg.js'

function loadSvg() {

    var draw = SVG().addTo('#svg-container').size(300, 300)
    var rect = draw.rect(100, 100).attr({ fill: '#f06' })

}