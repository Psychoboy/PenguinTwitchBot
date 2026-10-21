(function (global) {
    'use strict';

    function shipSvg(style, accent) {
        const hull = '#e8f3f0';
        const shapes = {
            arrow: '<polygon points="2,16 26,5 62,16 26,27" fill="' + hull + '"/><polygon points="22,16 40,11 52,16 40,21" fill="' + accent + '"/><rect x="0" y="13" width="6" height="6" fill="' + accent + '" opacity=".7"/>',
            hauler: '<rect x="10" y="8" width="46" height="16" rx="3" fill="' + hull + '"/><polygon points="56,8 63,16 56,24" fill="' + hull + '"/><rect x="3" y="11" width="8" height="10" fill="' + accent + '"/><rect x="38" y="11" width="14" height="10" fill="' + accent + '" opacity=".8"/>',
            scout: '<ellipse cx="34" cy="16" rx="24" ry="8" fill="' + hull + '"/><ellipse cx="42" cy="13" rx="8" ry="5" fill="' + accent + '"/><polygon points="12,16 2,5 2,27" fill="' + accent + '" opacity=".8"/>',
            cruiser: '<polygon points="4,12 54,12 63,16 54,20 4,20" fill="' + hull + '"/><rect x="20" y="4" width="14" height="8" rx="2" fill="' + hull + '"/><rect x="24" y="6" width="6" height="4" fill="' + accent + '"/><rect x="0" y="13" width="6" height="6" fill="' + accent + '" opacity=".7"/><rect x="36" y="14" width="18" height="4" fill="' + accent + '" opacity=".8"/>'
        };
        return '<svg viewBox="0 0 64 32" xmlns="http://www.w3.org/2000/svg">' + (shapes[style] || shapes.arrow) + '</svg>';
    }

    const OBJECT_ART = {
        wreck: [
            '<polygon points="14,40 56,30 62,46 50,58 18,56" fill="%H"/><polygon points="70,44 108,38 104,58 76,62" fill="%H"/><polygon points="56,30 62,46 50,58" fill="%D"/><circle cx="36" cy="46" r="5" fill="%A"/><polygon points="64,64 70,70 60,72" fill="%H"/><polygon points="84,24 90,30 80,32" fill="%H"/>',
            '<rect x="22" y="36" width="70" height="24" rx="6" fill="%H" transform="rotate(-12 57 48)"/><rect x="44" y="40" width="16" height="12" fill="%D" transform="rotate(-12 57 48)"/><line x1="86" y1="30" x2="100" y2="14" stroke="%A" stroke-width="2"/><circle cx="101" cy="13" r="3" fill="%A"/><polygon points="30,62 24,74 40,66" fill="%H"/>',
            '<polygon points="10,46 70,26 100,46 70,66" fill="%H"/><circle cx="68" cy="46" r="11" fill="%D"/><circle cx="68" cy="46" r="6" fill="%A" opacity=".8"/><rect x="100" y="40" width="10" height="12" fill="%H" transform="rotate(18 105 46)"/><polygon points="20,70 34,64 30,78" fill="%H" opacity=".7"/>'
        ],
        beacon: [
            '<rect x="56" y="30" width="8" height="46" fill="%H"/><circle cx="60" cy="26" r="7" fill="%A"/><circle cx="60" cy="26" r="14" fill="none" stroke="%A" stroke-width="1.5" opacity=".7"/><circle cx="60" cy="26" r="22" fill="none" stroke="%A" stroke-width="1.5" opacity=".4"/><rect x="46" y="74" width="28" height="6" fill="%H"/>',
            '<circle cx="60" cy="45" r="16" fill="%D" stroke="%H" stroke-width="2"/><circle cx="60" cy="45" r="8" fill="%A"/><ellipse cx="60" cy="45" rx="32" ry="9" fill="none" stroke="%A" stroke-width="1.5" opacity=".6"/><ellipse cx="60" cy="45" rx="44" ry="14" fill="none" stroke="%A" stroke-width="1" opacity=".35"/>',
            '<polygon points="40,64 80,64 70,76 50,76" fill="%H"/><path d="M34 44 Q60 70 86 44 Z" fill="%H"/><line x1="60" y1="52" x2="60" y2="24" stroke="%A" stroke-width="2"/><circle cx="60" cy="22" r="4" fill="%A"/><path d="M42 18 Q60 4 78 18" fill="none" stroke="%A" stroke-width="1.5" opacity=".6"/>'
        ],
        debris: [
            '<polygon points="20,50 38,34 54,44 48,64 26,66" fill="#7f8f8b"/><polygon points="64,30 84,24 94,42 78,52 62,46" fill="#9aa9a5"/><polygon points="70,62 92,60 98,74 76,78" fill="#6c7b77"/><polygon points="40,14 50,12 48,22" fill="#9aa9a5"/>',
            '<polygon points="30,70 40,22 52,66" fill="#9fd3e8"/><polygon points="50,72 66,14 80,70" fill="#bfe6f3"/><polygon points="76,70 90,34 100,68" fill="#8fc4da"/><circle cx="24" cy="30" r="3" fill="#bfe6f3"/>',
            '<rect x="18" y="30" width="34" height="20" fill="%H" transform="rotate(-18 35 40)"/><rect x="62" y="22" width="28" height="16" fill="%D" stroke="%H" transform="rotate(22 76 30)"/><rect x="50" y="56" width="40" height="12" fill="%H" transform="rotate(8 70 62)"/><circle cx="26" cy="64" r="5" fill="%A"/>'
        ],
        station: [
            '<circle cx="60" cy="45" r="30" fill="none" stroke="%H" stroke-width="7"/><circle cx="60" cy="45" r="10" fill="%A"/><line x1="60" y1="15" x2="60" y2="75" stroke="%H" stroke-width="3"/><line x1="30" y1="45" x2="90" y2="45" stroke="%H" stroke-width="3"/>',
            '<polygon points="56,10 64,10 68,62 52,62" fill="%H"/><rect x="38" y="62" width="44" height="8" fill="%H"/><rect x="46" y="72" width="28" height="6" fill="%D"/><circle cx="60" cy="24" r="5" fill="%A"/><rect x="28" y="40" width="64" height="5" fill="%H" opacity=".8"/>',
            '<rect x="48" y="33" width="24" height="24" fill="%H"/><rect x="18" y="39" width="26" height="12" fill="%H"/><rect x="76" y="39" width="26" height="12" fill="%H"/><rect x="52" y="8" width="16" height="22" fill="%H"/><rect x="52" y="60" width="16" height="22" fill="%H"/><circle cx="60" cy="45" r="6" fill="%A"/>'
        ],
        fork: [
            '<path d="M60 80 L60 52 L30 20" stroke="%H" stroke-width="6" fill="none"/><path d="M60 52 L90 20" stroke="%A" stroke-width="6" fill="none"/><polygon points="22,26 34,14 38,28" fill="%H"/><polygon points="98,26 86,14 82,28" fill="%A"/>',
            '<ellipse cx="60" cy="42" rx="16" ry="12" fill="#7f8f8b"/><path d="M60 82 Q24 66 24 34" stroke="%H" stroke-width="5" fill="none"/><path d="M60 82 Q96 66 96 34" stroke="%A" stroke-width="5" fill="none"/><circle cx="24" cy="28" r="5" fill="%H"/><circle cx="96" cy="28" r="5" fill="%A"/>',
            '<rect x="20" y="20" width="10" height="46" fill="%H"/><rect x="90" y="20" width="10" height="46" fill="%A"/><rect x="20" y="16" width="80" height="6" fill="%H" opacity=".5"/><path d="M60 82 L60 50 M60 50 L34 30 M60 50 L86 30" stroke="%H" stroke-width="4" fill="none"/><circle cx="60" cy="50" r="5" fill="%A"/>'
        ]
    };

    function objectSvg(kind, variant, accent) {
        const set = OBJECT_ART[kind];
        if (!set) return '';
        const index = Math.abs(parseInt(variant, 10) || 0) % set.length;
        const body = set[index].split('%H').join('#cfdcd9').split('%D').join('#17343a').split('%A').join(accent);
        return '<svg viewBox="0 0 120 90" xmlns="http://www.w3.org/2000/svg">' + body + '</svg>';
    }

    function variantCount(kind) {
        return OBJECT_ART[kind] ? OBJECT_ART[kind].length : 0;
    }

    global.StarshipArt = { shipSvg: shipSvg, objectSvg: objectSvg, variantCount: variantCount };
}(window));