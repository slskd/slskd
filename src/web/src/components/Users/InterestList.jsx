import React from 'react';
import { Icon } from 'semantic-ui-react';

// a wrapping list of interests.  items open with onSelect, or come off with
// onRemove; items in highlight are ones you like too
const InterestList = ({
  disabled = false,
  empty,
  highlight,
  items = [],
  onRemove,
  onSelect,
}) => {
  if (items.length === 0) {
    return empty ? <span className="user-profile-muted">{empty}</span> : null;
  }

  return (
    <ul className="interest-list">
      {items.map((item) => {
        const shared = highlight?.has(item);
        const title = shared ? `You like ${item} too` : undefined;

        return (
          <li
            className={`interest ${shared ? 'interest-shared' : ''}`}
            key={item}
          >
            {onSelect ? (
              <button
                className="interest-name"
                onClick={() => onSelect(item)}
                title={title ?? `Find users who like ${item}`}
                type="button"
              >
                {item}
              </button>
            ) : (
              <span
                className="interest-name"
                title={title}
              >
                {item}
              </span>
            )}
            {onRemove && (
              <button
                aria-label={`Remove ${item}`}
                className="interest-remove"
                disabled={disabled}
                onClick={() => onRemove(item)}
                title="Remove"
                type="button"
              >
                <Icon name="close" />
              </button>
            )}
          </li>
        );
      })}
    </ul>
  );
};

export default InterestList;
