import { normalize } from '../../lib/interests';
import { maxPictureBytes } from '../../lib/profile';
import * as users from '../../lib/users';
import { formatBytes } from '../../lib/util';
import InterestList from '../Users/InterestList';
import ProfileCard from '../Users/ProfileCard';
import React, { useEffect, useRef, useState } from 'react';
import { Button, Form, Icon, Input, Message, Segment } from 'semantic-ui-react';

const acceptedTypes = [
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/bmp',
  'image/webp',
];

const InterestEditor = ({
  disabled,
  icon,
  id,
  items,
  onAdd,
  onRemove,
  placeholder,
  title,
}) => {
  const [input, setInput] = useState('');

  const add = () => {
    const item = normalize(input);

    if (item) {
      onAdd(item);
    }

    setInput('');
  };

  return (
    <div className="settings-interests-column">
      <h4>
        <Icon name={icon} />
        {title}
        <span className="settings-interests-count">{items.length}</span>
      </h4>
      <Input
        action={{
          content: 'Add',
          disabled: disabled || !normalize(input),
          onClick: add,
          type: 'button',
        }}
        disabled={disabled}
        fluid
        id={id}
        onChange={(_event, { value }) => setInput(value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            add();
          }
        }}
        placeholder={placeholder}
        size="small"
        value={input}
      />
      <InterestList
        disabled={disabled}
        empty="Nothing yet."
        items={items}
        onRemove={onRemove}
      />
    </div>
  );
};

// edits the description, picture and interests served to other users, with a
// preview of how the profile looks to them.  the picture is only uploaded on save
const ProfileEditor = ({
  currentPictureUrl,
  description,
  descriptionChanged,
  disabled,
  hated,
  liked,
  onDescriptionChange,
  onInterestsChange,
  onPictureChange,
  picture,
  username,
}) => {
  const inputRef = useRef();
  const [dragging, setDragging] = useState(false);
  const [pictureError, setPictureError] = useState();
  const [statistics, setStatistics] = useState();

  useEffect(() => {
    if (!username) {
      return;
    }

    let cancelled = false;

    const load = async () => {
      try {
        const response = await users.getStatistics({ username });

        if (!cancelled) {
          setStatistics(response.data);
        }
      } catch {
        // the preview works without statistics
      }
    };

    load();

    // eslint-disable-next-line consistent-return
    return () => {
      cancelled = true;
    };
  }, [username]);

  const choose = (file) => {
    setPictureError(undefined);

    if (!file) {
      return;
    }

    if (!acceptedTypes.includes(file.type)) {
      setPictureError('Choose a JPEG, PNG, GIF, BMP or WebP image.');
      return;
    }

    if (file.size > maxPictureBytes) {
      setPictureError(
        `That image is ${formatBytes(file.size)}; the limit is ${formatBytes(maxPictureBytes)}.`,
      );
      return;
    }

    onPictureChange({ file, url: URL.createObjectURL(file) });
  };

  const previewUrl = picture?.remove
    ? undefined
    : picture?.url ?? currentPictureUrl;

  // an interest can't be both liked and disliked; adding it to one list takes
  // it off the other
  const addInterest = (list, item) => {
    const other = list === 'liked' ? 'hated' : 'liked';
    const lists = { hated: hated.items, liked: liked.items };
    const patch = {};

    if (!lists[list].includes(item)) {
      patch[list] = [...lists[list], item];
    }

    if (lists[other].includes(item)) {
      patch[other] = lists[other].filter((existing) => existing !== item);
    }

    onInterestsChange(patch);
  };

  const removeInterest = (list, item) => {
    const items = list === 'liked' ? liked.items : hated.items;
    onInterestsChange({
      [list]: items.filter((existing) => existing !== item),
    });
  };

  return (
    <div className="settings-profile">
      <Segment className="settings-group">
        <h3>Your profile</h3>
        <Form>
          <Form.Field>
            <label htmlFor="settings-profile-description">
              Description
              {descriptionChanged && (
                <span
                  aria-label="Unsaved change"
                  className="settings-changed-dot"
                  title="Unsaved change"
                />
              )}
            </label>
            <Form.TextArea
              disabled={disabled}
              id="settings-profile-description"
              onChange={(_event, { value }) => onDescriptionChange(value)}
              placeholder="Tell other users about yourself and what you share"
              rows={8}
              value={description ?? ''}
            />
            <div className="settings-field-help">
              {(description ?? '').length.toLocaleString()} characters. Links
              are clickable for people using this app.
            </div>
          </Form.Field>
          <Form.Field>
            <label htmlFor="settings-profile-picture">
              Picture
              {picture && (
                <span
                  aria-label="Unsaved change"
                  className="settings-changed-dot"
                  title="Unsaved change"
                />
              )}
            </label>
            {/* eslint-disable-next-line jsx-a11y/no-static-element-interactions */}
            <div
              className={`settings-dropzone ${dragging ? 'dragging' : ''} ${
                disabled ? 'disabled' : ''
              }`}
              onDragLeave={(event) => {
                // moving between the zone's own children also fires dragleave
                if (!event.currentTarget.contains(event.relatedTarget)) {
                  setDragging(false);
                }
              }}
              onDragOver={(event) => {
                event.preventDefault();

                if (!disabled) {
                  setDragging(true);
                }
              }}
              onDrop={(event) => {
                event.preventDefault();
                setDragging(false);

                if (!disabled) {
                  choose(event.dataTransfer.files?.[0]);
                }
              }}
            >
              {previewUrl ? (
                <img
                  alt="Your profile"
                  className="settings-dropzone-preview"
                  src={previewUrl}
                />
              ) : (
                <Icon
                  className="settings-dropzone-icon"
                  name="image outline"
                  size="huge"
                />
              )}
              <div className="settings-dropzone-text">
                <p>
                  {picture?.file
                    ? `${picture.file.name} (${formatBytes(picture.file.size)}), saved when you save settings`
                    : 'Drop an image here, or choose one.'}
                </p>
                <div className="settings-dropzone-buttons">
                  <Button
                    content={previewUrl ? 'Replace…' : 'Choose Image…'}
                    disabled={disabled}
                    icon="upload"
                    onClick={() => inputRef.current.click()}
                    size="small"
                    type="button"
                  />
                  {previewUrl && (
                    <Button
                      content="Remove"
                      disabled={disabled}
                      icon="trash alternate outline"
                      onClick={() => onPictureChange({ remove: true })}
                      size="small"
                      type="button"
                    />
                  )}
                  {picture && (
                    <Button
                      content="Undo"
                      disabled={disabled}
                      icon="undo"
                      onClick={() => onPictureChange(undefined)}
                      size="small"
                      type="button"
                    />
                  )}
                </div>
                <div className="settings-field-help">
                  JPEG, PNG, GIF, BMP or WebP, up to{' '}
                  {formatBytes(maxPictureBytes)}. Smaller images load faster for
                  other users.
                </div>
              </div>
              <input
                accept={acceptedTypes.join(',')}
                hidden
                id="settings-profile-picture"
                onChange={(event) => {
                  choose(event.target.files?.[0]);
                  event.target.value = '';
                }}
                ref={inputRef}
                type="file"
              />
            </div>
            {pictureError && (
              <Message
                negative
                size="small"
              >
                {pictureError}
              </Message>
            )}
          </Form.Field>
          <Form.Field>
            <label htmlFor="settings-profile-likes">
              Interests
              {(liked.changed || hated.changed) && (
                <span
                  aria-label="Unsaved change"
                  className="settings-changed-dot"
                  title="Unsaved change"
                />
              )}
            </label>
            <div className="settings-interests">
              <InterestEditor
                disabled={disabled}
                icon="thumbs up outline"
                id="settings-profile-likes"
                items={liked.items}
                onAdd={(item) => addInterest('liked', item)}
                onRemove={(item) => removeInterest('liked', item)}
                placeholder="An artist, genre or anything else"
                title="Likes"
              />
              <InterestEditor
                disabled={disabled}
                icon="thumbs down outline"
                id="settings-profile-dislikes"
                items={hated.items}
                onAdd={(item) => addInterest('hated', item)}
                onRemove={(item) => removeInterest('hated', item)}
                placeholder="Something you'd rather avoid"
                title="Dislikes"
              />
            </div>
            <div className="settings-field-help">
              Other users can find you by the things you like, and see both
              lists on your profile. Interests are saved in lowercase, like
              other clients do.
            </div>
          </Form.Field>
        </Form>
      </Segment>
      <Segment className="settings-group settings-profile-preview">
        <h3>How other users see you</h3>
        <ProfileCard
          description={description}
          infoLoading={false}
          interests={{ hated: hated.items, liked: liked.items }}
          picture={previewUrl}
          presence="Online"
          statistics={statistics}
          username={username ?? 'you'}
        />
      </Segment>
    </div>
  );
};

export default ProfileEditor;
