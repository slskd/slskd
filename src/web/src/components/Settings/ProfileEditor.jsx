import { maxPictureBytes } from '../../lib/profile';
import * as users from '../../lib/users';
import { formatBytes } from '../../lib/util';
import ProfileCard from '../Users/ProfileCard';
import React, { useEffect, useRef, useState } from 'react';
import { Button, Form, Icon, Message, Segment } from 'semantic-ui-react';

const acceptedTypes = [
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/bmp',
  'image/webp',
];

// edits the description and picture served to other users, with a preview of
// how the profile looks to them.  the picture is only uploaded on save
const ProfileEditor = ({
  currentPictureUrl,
  description,
  descriptionChanged,
  disabled,
  onDescriptionChange,
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
        </Form>
      </Segment>
      <Segment className="settings-group settings-profile-preview">
        <h3>How other users see you</h3>
        <ProfileCard
          description={description}
          infoLoading={false}
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
