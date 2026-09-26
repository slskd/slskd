import './ReleaseIcon.css';
import React from 'react';

// a record half out of its sleeve, drawn to sit in place of an icon font glyph

const circle = (radius) =>
  `M${15 - radius},12a${radius},${radius} 0 1,0 ${2 * radius},0a${radius},${radius} 0 1,0 ${-2 * radius},0`;

// the disc, with a groove and a spindle hole cut out
const disc = [8, 5.7, 5, 1.3].map(circle).join('');

// the sleeve behind it, cut away in an arc so there's a gap around the disc
const sleeve =
  'M3,3.5H11.48A9.2,9.2 0 0,0 11.48,20.5H3A1.5,1.5 0 0,1 1.5,19V5A1.5,1.5 0 0,1 3,3.5Z';

const ReleaseIcon = ({ className = '', size = '' }) => (
  <i
    aria-hidden="true"
    className={`${size} icon release-icon ${className}`}
  >
    <svg
      focusable="false"
      viewBox="0 0 24 24"
    >
      <path d={sleeve} />
      <path
        d={disc}
        fillRule="evenodd"
      />
    </svg>
  </i>
);

export default ReleaseIcon;
