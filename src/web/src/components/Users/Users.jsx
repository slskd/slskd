import './Users.css';
import '../UserPanel/UserPanel.css';
import { activeUserInfoKey, urlBase } from '../../config';
import AppContext from '../AppContext';
import PlaceholderSegment from '../Shared/PlaceholderSegment';
import Interests, { interestsPath } from './Interests';
import UserView from './UserView';
import React, { useContext, useEffect, useState } from 'react';
import {
  useHistory,
  useLocation,
  useParams,
  useRouteMatch,
} from 'react-router-dom';
import { Button, Icon, Input, Segment } from 'semantic-ui-react';

const tabsKey = 'slskd-user-tabs';

const loadTabs = () => {
  try {
    const saved = JSON.parse(localStorage.getItem(tabsKey));
    return Array.isArray(saved) ? saved.filter(Boolean) : [];
  } catch {
    return [];
  }
};

const userPath = (username) =>
  `${urlBase}/users/${encodeURIComponent(username)}`;

// open user profiles as tabs, like nicotine+'s user info tabs, next to a
// pinned tab for finding users by interest
const Users = () => {
  const history = useHistory();
  const location = useLocation();
  const { username: active } = useParams();
  const interestsActive = Boolean(useRouteMatch(`${urlBase}/interests`));
  const { state = {} } = useContext(AppContext) ?? {};
  const selfUsername = state.user?.username;

  const [tabs, setTabs] = useState(loadTabs);
  const [views, setViews] = useState({});
  const [input, setInput] = useState('');

  // older links pass the user in location state; the last viewed user is
  // restored when landing on the page without one, including from the nav
  // while a user is showing
  useEffect(() => {
    const requested = location.state?.user;

    if (requested) {
      history.replace(userPath(requested));
    } else if (!active && !interestsActive) {
      const last = localStorage.getItem(activeUserInfoKey);
      const restore = tabs.includes(last) ? last : tabs[0];

      if (restore) {
        history.replace(userPath(restore));
      }
    }
  }, [location.state, active, interestsActive]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!active) {
      return;
    }

    localStorage.setItem(activeUserInfoKey, active);
    setTabs((previous) =>
      previous.includes(active) ? previous : [...previous, active],
    );
  }, [active]);

  useEffect(() => {
    localStorage.setItem(tabsKey, JSON.stringify(tabs));
  }, [tabs]);

  const open = (username) => {
    const trimmed = username?.trim();

    if (trimmed) {
      history.push(userPath(trimmed));
      setInput('');
    }
  };

  const close = (username) => {
    const index = tabs.indexOf(username);
    const next = tabs.filter((tab) => tab !== username);
    setTabs(next);

    if (username === active) {
      const neighbor = next[index] ?? next[index - 1];
      history.replace(neighbor ? userPath(neighbor) : `${urlBase}/users`);
    }
  };

  const view = views[active] ?? { refreshKey: 0, tab: 'profile' };
  const setView = (patch) =>
    setViews((previous) => ({
      ...previous,
      [active]: { ...view, ...patch },
    }));

  return (
    <div className="users-container">
      <Segment
        className="users-segment"
        raised
      >
        <div className="users-segment-icon">
          <Icon
            name="users"
            size="big"
          />
        </div>
        <Input
          action={{
            'aria-label': 'Open user',
            disabled: !input.trim(),
            icon: 'search',
            onClick: () => open(input),
          }}
          className="users-input"
          input={
            <input
              aria-label="Username"
              data-lpignore="true"
              placeholder="Open a user's profile by username"
              type="search"
            />
          }
          onChange={(_event, { value }) => setInput(value)}
          onKeyUp={(event) => event.key === 'Enter' && open(input)}
          size="big"
          value={input}
        />
        {selfUsername && (
          <Button
            className="users-self-button"
            content="My Profile"
            icon="id card"
            onClick={() => open(selfUsername)}
            size="big"
          />
        )}
      </Segment>
      <div
        aria-label="Open users"
        className="users-tabs"
        role="tablist"
      >
        <div
          className={`users-tab users-tab-pinned ${
            interestsActive ? 'active' : ''
          }`}
        >
          <button
            aria-selected={interestsActive}
            className="users-tab-name"
            onClick={() => history.push(interestsPath())}
            role="tab"
            title="Find users by interest"
            type="button"
          >
            <Icon name="heart outline" />
            Interests
          </button>
        </div>
        {tabs.map((username) => (
          <div
            className={`users-tab ${username === active ? 'active' : ''}`}
            key={username}
          >
            <button
              aria-selected={username === active}
              className="users-tab-name"
              onAuxClick={(event) => event.button === 1 && close(username)}
              onClick={() => history.push(userPath(username))}
              role="tab"
              title={`${username} (middle click to close)`}
              type="button"
            >
              {username === selfUsername && (
                <Icon
                  name="id card outline"
                  title="You"
                />
              )}
              {username}
            </button>
            <button
              aria-label={`Close ${username}`}
              className="users-tab-close"
              onClick={() => close(username)}
              title="Close"
              type="button"
            >
              <Icon name="close" />
            </button>
          </div>
        ))}
        {tabs.length > 1 && (
          <button
            className="users-tab-close-all"
            onClick={() => {
              setTabs([]);

              if (!interestsActive) {
                history.replace(`${urlBase}/users`);
              }
            }}
            type="button"
          >
            Close all
          </button>
        )}
      </div>
      {interestsActive ? (
        <Segment
          className="users-user"
          raised
        >
          <Interests />
        </Segment>
      ) : active ? (
        <Segment
          className="users-user"
          raised
        >
          <div className="users-user-header">
            <h2>{active}</h2>
            {/* the files tab has its own refresh */}
            {view.tab === 'profile' && (
              <Button
                aria-label="Refresh profile"
                icon="refresh"
                onClick={() => setView({ refreshKey: view.refreshKey + 1 })}
                size="small"
                title="Refresh profile"
              />
            )}
          </div>
          <UserView
            key={active}
            onTabChange={(tab) => setView({ tab })}
            refreshKey={view.refreshKey}
            tab={view.tab}
            username={active}
          />
        </Segment>
      ) : (
        <PlaceholderSegment
          caption="Open a profile by username, or click any username in search results, rooms, chat or transfers"
          icon="users"
        />
      )}
    </div>
  );
};

export default Users;
