import { formatSpeed } from '../../lib/util';
import InterestList from './InterestList';
import React, { useState } from 'react';
import { Icon, Label, Loader, Modal, Popup } from 'semantic-ui-react';

const presenceColors = {
  Away: 'yellow',
  Online: 'green',
};

export const PresenceIcon = ({ presence }) => (
  <Icon
    aria-label={presence ?? 'Unknown status'}
    color={presenceColors[presence] || 'grey'}
    name="circle"
    title={presence ?? 'Unknown status'}
  />
);

const urlPattern = /(https?:\/\/[^\s<>"']+)/gu;

// descriptions are plain text, but people put links in them
const Linkified = ({ text }) =>
  text.split(urlPattern).map((part, index) =>
    index % 2 === 1 ? (
      <a
        href={part}
        // eslint-disable-next-line react/no-array-index-key
        key={index}
        rel="noopener noreferrer"
        target="_blank"
      >
        {part}
      </a>
    ) : (
      part
    ),
  );

const Picture = ({ picture, username }) => {
  const [zoomed, setZoomed] = useState(false);

  if (!picture) {
    return (
      <div
        aria-label="No picture"
        className="user-profile-picture user-profile-picture-empty"
        role="img"
      >
        <Icon
          name="user"
          size="huge"
        />
      </div>
    );
  }

  return (
    <>
      <button
        className="user-profile-picture"
        onClick={() => setZoomed(true)}
        title="View full size"
        type="button"
      >
        <img
          alt={username}
          src={picture}
        />
      </button>
      <Modal
        basic
        className="user-profile-picture-modal"
        closeIcon
        onClose={() => setZoomed(false)}
        open={zoomed}
        size="large"
      >
        <Modal.Content>
          <img
            alt={`${username}, full size`}
            src={picture}
          />
        </Modal.Content>
        <Modal.Actions>
          <a
            className="ui basic inverted button"
            download={username}
            href={picture}
          >
            <Icon name="download" />
            Save Picture
          </a>
        </Modal.Actions>
      </Modal>
    </>
  );
};

const Stat = ({ children, label }) => (
  <>
    <dt>{label}</dt>
    <dd>{children}</dd>
  </>
);

const formatCount = (value) =>
  value == null ? '–' : Number(value).toLocaleString();

const Interests = ({
  interests,
  interestsError,
  interestsLoading,
  onInterestSelect,
  sharedInterests,
}) => {
  const liked = interests?.liked ?? [];
  const hated = interests?.hated ?? [];

  let content;

  if (interestsLoading) {
    content = <span className="user-profile-muted">Requesting interests…</span>;
  } else if (interestsError) {
    content = <span className="user-profile-muted">{interestsError}</span>;
  } else if (liked.length === 0 && hated.length === 0) {
    content = <span className="user-profile-muted">No interests shared.</span>;
  } else {
    content = (
      <div className="user-profile-interest-groups">
        <div>
          <h4>
            <Icon name="thumbs up outline" />
            Likes
          </h4>
          <InterestList
            empty="Nothing yet."
            highlight={sharedInterests}
            items={liked}
            onSelect={onInterestSelect}
          />
        </div>
        <div>
          <h4>
            <Icon name="thumbs down outline" />
            Dislikes
          </h4>
          <InterestList
            empty="Nothing yet."
            items={hated}
            onSelect={onInterestSelect}
          />
        </div>
      </div>
    );
  }

  return <div className="user-profile-interests">{content}</div>;
};

// the information another user would see in nicotine+'s user info tab
const ProfileCard = ({
  description,
  endpoint,
  group,
  info,
  infoError,
  infoLoading,
  interests,
  interestsError,
  interestsLoading,
  isPrivileged,
  onInterestSelect,
  picture,
  presence,
  sharedInterests,
  statistics,
  title,
  username,
}) => {
  const hasInfo = info != null;

  return (
    <div className="user-profile-card">
      <div className="user-profile-summary">
        {infoLoading ? (
          <div className="user-profile-picture user-profile-picture-empty">
            <Loader
              active
              inline
              size="small"
            />
          </div>
        ) : (
          <Picture
            picture={picture}
            username={username}
          />
        )}
        <div className="user-profile-details">
          <div className="user-profile-name">
            <PresenceIcon presence={presence} />
            <span>{title ?? username}</span>
            {isPrivileged && (
              <Popup
                content="Privileged user"
                trigger={
                  <Icon
                    aria-label="Privileged"
                    color="yellow"
                    name="star"
                  />
                }
              />
            )}
            {group && group !== 'default' && (
              <Label
                basic
                color={group === 'blacklisted' ? 'red' : undefined}
                size="tiny"
              >
                {group}
              </Label>
            )}
          </div>
          <dl className="user-profile-stats">
            <Stat label="Status">{presence ?? 'Unknown'}</Stat>
            <Stat label="Shared">
              {statistics
                ? `${formatCount(statistics.fileCount)} files in ${formatCount(
                    statistics.directoryCount,
                  )} folders`
                : '–'}
            </Stat>
            <Stat label="Upload speed">
              {statistics?.averageSpeed
                ? `${formatSpeed(statistics.averageSpeed)}`
                : '–'}
            </Stat>
            {hasInfo && (
              <>
                <Stat label="Upload slots">
                  {formatCount(info.uploadSlots)}
                  {info.hasFreeUploadSlot ? (
                    <Label
                      basic
                      color="green"
                      size="tiny"
                    >
                      free slot
                    </Label>
                  ) : (
                    <Label
                      basic
                      size="tiny"
                    >
                      no free slot
                    </Label>
                  )}
                </Stat>
                <Stat label="Queued uploads">
                  {formatCount(info.queueLength)}
                </Stat>
              </>
            )}
            {endpoint?.address && (
              <Stat label="Address">
                <span className="user-profile-mono">
                  {endpoint.address}:{endpoint.port}
                </span>
              </Stat>
            )}
          </dl>
        </div>
      </div>
      <div className="user-profile-description">
        {infoLoading ? (
          <span className="user-profile-muted">Requesting user info…</span>
        ) : infoError ? (
          <span className="user-profile-muted">{infoError}</span>
        ) : description ? (
          <Linkified text={description} />
        ) : (
          <span className="user-profile-muted">No description.</span>
        )}
      </div>
      <Interests
        interests={interests}
        interestsError={interestsError}
        interestsLoading={interestsLoading}
        onInterestSelect={onInterestSelect}
        sharedInterests={sharedInterests}
      />
    </div>
  );
};

export default ProfileCard;
