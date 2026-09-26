import { urlBase } from '../../config';
import * as interests from '../../lib/interests';
import AppContext from '../AppContext';
import InterestList from './InterestList';
import { getErrorMessage } from './UserProfile';
import React, { useContext, useEffect, useMemo, useState } from 'react';
import { Link, useHistory, useLocation } from 'react-router-dom';
import { Icon, Input, Loader } from 'semantic-ui-react';

export const interestsPath = (item) =>
  item
    ? `${urlBase}/interests?item=${encodeURIComponent(item)}`
    : `${urlBase}/interests`;

const userPath = (username) =>
  `${urlBase}/users/${encodeURIComponent(username)}`;

// runs request when deps change, keeping only the latest result
const useRequest = (request, deps) => {
  const [result, setResult] = useState({ loading: true });

  useEffect(() => {
    let cancelled = false;
    setResult({ loading: true });

    const run = async () => {
      try {
        const data = await request();

        if (!cancelled) {
          setResult({ data, loading: false });
        }
      } catch (error) {
        if (!cancelled) {
          setResult({ error: getErrorMessage(error), loading: false });
        }
      }
    };

    run();

    return () => {
      cancelled = true;
    };
  }, deps); // eslint-disable-line react-hooks/exhaustive-deps

  return result;
};

const byName = (a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' });

const Section = ({ children, count, icon, title }) => (
  <section className="interests-section">
    <h3>
      <Icon name={icon} />
      {title}
      {count != null && <span className="interests-count">{count}</span>}
    </h3>
    {children}
  </section>
);

const Pending = ({ children, error, loading }) => {
  if (loading) {
    return (
      <Loader
        active
        inline
        size="small"
      />
    );
  }

  if (error) {
    return <span className="user-profile-muted">{error}</span>;
  }

  return children;
};

// popular interests have hundreds of fans; show a screenful until asked
const userLimit = 60;

// the server returns every related interest it knows, negatives included
const recommendationLimit = 40;

const UserList = ({ empty, selfUsername, usernames }) => {
  const history = useHistory();
  const [expanded, setExpanded] = useState(false);

  if (usernames.length === 0) {
    return <span className="user-profile-muted">{empty}</span>;
  }

  const shown = expanded ? usernames : usernames.slice(0, userLimit);

  return (
    <>
      <ul className="interests-users">
        {shown.map((username) => (
          <li key={username}>
            <button
              onClick={() => history.push(userPath(username))}
              title={`Open ${username}'s profile`}
              type="button"
            >
              <Icon name="user outline" />
              {username}
              {username === selfUsername && (
                <span className="user-profile-muted"> (you)</span>
              )}
            </button>
          </li>
        ))}
      </ul>
      {shown.length < usernames.length && (
        <button
          className="interests-more"
          onClick={() => setExpanded(true)}
          type="button"
        >
          Show all {usernames.length.toLocaleString()}
        </button>
      )}
    </>
  );
};

const RecommendationList = ({ empty, liked, onSelect, recommendations }) => {
  const items = useMemo(
    () =>
      (recommendations ?? [])
        .filter((entry) => entry.score > 0)
        .sort((a, b) => b.score - a.score)
        .slice(0, recommendationLimit)
        .map((entry) => entry.item),
    [recommendations],
  );

  return (
    <InterestList
      empty={empty}
      highlight={liked}
      items={items}
      onSelect={onSelect}
    />
  );
};

const ItemResults = ({ item, liked, onSelect, selfUsername }) => {
  const users = useRequest(() => interests.getUsersWhoLike({ item }), [item]);
  const related = useRequest(
    () => interests.getRecommendations({ item }),
    [item],
  );

  const usernames = useMemo(
    () => [...(users.data ?? [])].sort(byName),
    [users.data],
  );

  return (
    <>
      <Section
        count={users.data ? usernames.length : undefined}
        icon="users"
        title={`People who like ${item}`}
      >
        <Pending {...users}>
          <UserList
            empty={`Nobody online likes ${item} right now. Interests only count while their owners are connected.`}
            selfUsername={selfUsername}
            usernames={usernames}
          />
        </Pending>
      </Section>
      <Section
        icon="linkify"
        title="Related interests"
      >
        <Pending {...related}>
          <RecommendationList
            empty="No related interests."
            liked={liked}
            onSelect={onSelect}
            recommendations={related.data?.recommended}
          />
        </Pending>
      </Section>
    </>
  );
};

const Overview = ({ liked, onSelect, selfUsername }) => {
  const hasLikes = liked.size > 0;

  const similar = useRequest(
    () => (hasLikes ? interests.getSimilarUsers() : Promise.resolve([])),
    [hasLikes],
  );
  const recommended = useRequest(
    () => (hasLikes ? interests.getRecommendations() : Promise.resolve()),
    [hasLikes],
  );
  const popular = useRequest(() => interests.getGlobalRecommendations(), []);

  const similarUsernames = useMemo(
    () =>
      (similar.data ?? [])
        .filter((user) => user.username !== selfUsername)
        .sort((a, b) => b.rating - a.rating || byName(a.username, b.username))
        .map((user) => user.username),
    [similar.data, selfUsername],
  );

  return (
    <>
      <Section
        icon="thumbs up outline"
        title="Your likes"
      >
        {hasLikes ? (
          <InterestList
            items={[...liked]}
            onSelect={onSelect}
          />
        ) : (
          <span className="user-profile-muted">
            You haven&apos;t added any interests.{' '}
            <Link to={`${urlBase}/settings/profile`}>Add some</Link> so people
            with the same taste can find you, and to see who&apos;s like you.
          </span>
        )}
      </Section>
      {hasLikes && (
        <>
          <Section
            count={similar.data ? similarUsernames.length : undefined}
            icon="users"
            title="People like you"
          >
            <Pending {...similar}>
              <UserList
                empty="Nobody online shares your interests right now."
                selfUsername={selfUsername}
                usernames={similarUsernames}
              />
            </Pending>
          </Section>
          <Section
            icon="lightbulb outline"
            title="Recommended for you"
          >
            <Pending {...recommended}>
              <RecommendationList
                empty="No recommendations yet."
                liked={liked}
                onSelect={onSelect}
                recommendations={recommended.data?.recommended}
              />
            </Pending>
          </Section>
        </>
      )}
      <Section
        icon="chart line"
        title="Popular on the network"
      >
        <Pending {...popular}>
          <RecommendationList
            empty="Nothing to show."
            liked={liked}
            onSelect={onSelect}
            recommendations={popular.data?.recommended}
          />
        </Pending>
      </Section>
    </>
  );
};

// find people by what they like, like nicotine+'s interests tab
const Interests = () => {
  const history = useHistory();
  const location = useLocation();
  const { options = {}, state = {} } = useContext(AppContext) ?? {};
  const selfUsername = state.user?.username;
  const item = interests.normalize(
    new URLSearchParams(location.search).get('item'),
  );
  const [input, setInput] = useState(item);

  useEffect(() => {
    setInput(item);
  }, [item]);

  const likedOption = options.soulseek?.interests?.liked;
  const liked = useMemo(() => new Set(likedOption ?? []), [likedOption]);

  const select = (next) => {
    const normalized = interests.normalize(next);

    if (normalized !== item) {
      history.push(interestsPath(normalized));
    }
  };

  return (
    <div className="interests">
      <form
        className="interests-search"
        onSubmit={(event) => {
          event.preventDefault();
          select(input);
        }}
      >
        <Input
          action={{
            'aria-label': 'Find users',
            disabled: !interests.normalize(input),
            icon: 'search',
            type: 'submit',
          }}
          fluid
          input={
            <input
              aria-label="Interest"
              data-lpignore="true"
              placeholder="Find people who like… (an artist, a genre, anything)"
              type="search"
            />
          }
          onChange={(_event, { value }) => setInput(value)}
          value={input}
        />
        {item && (
          <button
            className="interests-back"
            onClick={() => history.push(interestsPath())}
            type="button"
          >
            <Icon name="arrow left" />
            Overview
          </button>
        )}
      </form>
      {item ? (
        <ItemResults
          item={item}
          liked={liked}
          onSelect={select}
          selfUsername={selfUsername}
        />
      ) : (
        <Overview
          liked={liked}
          onSelect={select}
          selfUsername={selfUsername}
        />
      )}
    </div>
  );
};

export default Interests;
