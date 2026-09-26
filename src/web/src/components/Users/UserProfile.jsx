import { urlBase } from '../../config';
import * as groups from '../../lib/groups';
import * as profile from '../../lib/profile';
import * as users from '../../lib/users';
import { copyToClipboard } from '../../lib/util';
import AppContext from '../AppContext';
import { useUserPanel } from '../UserPanel/UserPanelContext';
import ProfileCard from './ProfileCard';
import React, { useContext, useEffect, useMemo, useState } from 'react';
import { useHistory } from 'react-router-dom';
import { toast } from 'react-toastify';
import {
  Button,
  Confirm,
  Dropdown,
  Form,
  Icon,
  Message,
  Modal,
} from 'semantic-ui-react';

const pictureMimeTypes = [
  ['/9j/', 'image/jpeg'],
  ['iVBOR', 'image/png'],
  ['R0lG', 'image/gif'],
  ['UklGR', 'image/webp'],
  ['Qk', 'image/bmp'],
];

const toPictureUrl = (base64) => {
  const mime =
    pictureMimeTypes.find(([prefix]) => base64.startsWith(prefix))?.[1] ??
    'image';
  return `data:${mime};base64,${base64}`;
};

export const getErrorMessage = (error) => {
  const data = error?.response?.data;

  if (typeof data === 'string' && data.length > 0) {
    return data;
  }

  return data?.title ?? error?.message ?? String(error);
};

// loads a profile in stages: server-side data (status, statistics) arrives
// quickly, while user info needs a peer connection and can take a while
const useUserProfile = ({ isSelf, refreshKey, username }) => {
  const [data, setData] = useState({ infoLoading: true });

  useEffect(() => {
    let cancelled = false;
    let objectUrl;
    const update = (patch) =>
      !cancelled && setData((previous) => ({ ...previous, ...patch }));

    setData({ infoLoading: true, interestsLoading: !isSelf });

    // interests come from the server rather than the peer, so they load on
    // their own instead of waiting behind user info
    const loadInterests = async () => {
      try {
        update({
          interests: await users.getInterests({ username }),
          interestsLoading: false,
        });
      } catch (error) {
        update({
          interestsError: `Couldn't get interests: ${getErrorMessage(error)}`,
          interestsLoading: false,
        });
      }
    };

    const load = async () => {
      const [status, statistics, group] = await Promise.allSettled([
        users.getStatus({ username }),
        users.getStatistics({ username }),
        users.getGroup({ username }),
      ]);

      const presence = status.value?.data?.presence;

      update({
        group: group.value,
        isPrivileged: status.value?.data?.isPrivileged,
        presence,
        statistics: statistics.value?.data,
      });

      if (isSelf) {
        try {
          objectUrl = await profile.getPictureUrl();

          if (cancelled && objectUrl) {
            URL.revokeObjectURL(objectUrl);
          }

          update({ infoLoading: false, picture: objectUrl });
        } catch {
          update({ infoLoading: false });
        }

        return;
      }

      if (group.value === 'blacklisted') {
        update({
          infoError:
            'You have banned this user. Unban them to see their profile.',
          infoLoading: false,
          interestsLoading: false,
        });
        return;
      }

      loadInterests();

      if (presence === 'Offline') {
        update({ infoError: `${username} is offline.`, infoLoading: false });
        return;
      }

      const [info, endpoint] = await Promise.allSettled([
        users.getInfo({ username }),
        users.getEndpoint({ username }),
      ]);

      update({
        endpoint: endpoint.value?.data,
        info: info.value?.data,
        infoError:
          info.status === 'rejected'
            ? `Couldn't get user info: ${getErrorMessage(info.reason)}`
            : undefined,
        infoLoading: false,
        picture: info.value?.data?.hasPicture
          ? toPictureUrl(info.value.data.picture)
          : undefined,
      });
    };

    load();

    return () => {
      cancelled = true;

      if (objectUrl) {
        URL.revokeObjectURL(objectUrl);
      }
    };
  }, [isSelf, refreshKey, username]);

  return data;
};

const GiftPrivilegesModal = ({ onClose, open, username }) => {
  const [days, setDays] = useState('7');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState();
  const parsedDays = Number.parseInt(days, 10);
  const valid = Number.isInteger(parsedDays) && parsedDays > 0;

  const gift = async () => {
    setPending(true);
    setError(undefined);

    try {
      await users.grantPrivileges({ days: parsedDays, username });
      toast.success(
        `Gifted ${parsedDays} day${parsedDays === 1 ? '' : 's'} of privileges to ${username}`,
      );
      onClose();
    } catch (giftError) {
      setError(getErrorMessage(giftError));
    } finally {
      setPending(false);
    }
  };

  return (
    <Modal
      onClose={onClose}
      open={open}
      size="tiny"
    >
      <Modal.Header>
        <Icon name="gift" />
        Gift Privileges to {username}
      </Modal.Header>
      <Modal.Content>
        <p>
          Days are taken from your own privileges. This can&apos;t be undone.
        </p>
        <Form
          error={Boolean(error)}
          onSubmit={() => valid && gift()}
        >
          <Form.Input
            autoFocus
            label="Days"
            min={1}
            onChange={(_event, { value }) => setDays(value)}
            type="number"
            value={days}
          />
          <Message
            content={error}
            error
          />
        </Form>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          disabled={!valid || pending}
          loading={pending}
          onClick={gift}
          primary
        >
          Gift {valid ? parsedDays : ''} Day{parsedDays === 1 ? '' : 's'}
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

const UserActions = ({ group, onBrowse, onChanged, username }) => {
  const { options = {} } = useContext(AppContext) ?? {};
  const panel = useUserPanel();
  const [confirmBan, setConfirmBan] = useState(false);
  const [giftOpen, setGiftOpen] = useState(false);
  const [pending, setPending] = useState(false);

  const canConfigure = Boolean(options.remoteConfiguration);
  const configureHint = canConfigure
    ? undefined
    : 'Enable remote configuration to change this from the web UI';
  const banned = groups.isBanned(options, username);
  const userDefinedGroups = groups.getUserDefinedGroups(options);
  const memberships = groups.getMemberships(options, username);

  const run = async (action, successMessage) => {
    setPending(true);

    try {
      await action();
      toast.success(successMessage);
      // the server reloads the file asynchronously; give it a moment
      setTimeout(onChanged, 1_500);
    } catch (error) {
      toast.error(getErrorMessage(error));
    } finally {
      setPending(false);
    }
  };

  const toggleGroup = (name) =>
    memberships.includes(name)
      ? run(
          () => groups.removeFromGroup({ group: name, username }),
          `Removed ${username} from ${name}`,
        )
      : run(
          () => groups.addToGroup({ group: name, username }),
          `Added ${username} to ${name}`,
        );

  return (
    <div className="user-profile-actions">
      <Button
        content="Browse Files"
        disabled={group === 'blacklisted'}
        icon="folder open"
        onClick={onBrowse}
        primary
        size="small"
      />
      <Button
        content="Message"
        disabled={!panel}
        icon="comment"
        onClick={() => panel?.composeMessage(username)}
        size="small"
      />
      <Dropdown
        button
        className="small icon"
        disabled={pending}
        floating
        icon="ellipsis horizontal"
        labeled={false}
        loading={pending}
        text="More "
      >
        <Dropdown.Menu>
          <Dropdown.Item
            icon="copy"
            onClick={async () => {
              await copyToClipboard(username);
              toast.info(`Copied ${username}`);
            }}
            text="Copy Username"
          />
          <Dropdown.Item
            icon="gift"
            onClick={() => setGiftOpen(true)}
            text="Gift Privileges…"
          />
          <Dropdown.Divider />
          <Dropdown.Header
            content="Groups"
            icon="users"
          />
          {userDefinedGroups.length === 0 ? (
            <Dropdown.Item
              description="Define groups in Settings"
              disabled
              text="No groups"
            />
          ) : (
            userDefinedGroups.map(({ name }) => (
              <Dropdown.Item
                disabled={!canConfigure}
                icon={
                  memberships.includes(name) ? 'check square' : 'square outline'
                }
                key={name}
                onClick={() => toggleGroup(name)}
                text={name}
                title={configureHint}
              />
            ))
          )}
          <Dropdown.Divider />
          {banned ? (
            <Dropdown.Item
              disabled={!canConfigure}
              icon="undo"
              onClick={() =>
                run(() => groups.unban({ username }), `Unbanned ${username}`)
              }
              text="Unban User"
              title={configureHint}
            />
          ) : (
            <Dropdown.Item
              className="user-profile-danger"
              disabled={!canConfigure}
              icon="ban"
              onClick={() => setConfirmBan(true)}
              text="Ban User…"
              title={configureHint}
            />
          )}
        </Dropdown.Menu>
      </Dropdown>
      <Confirm
        cancelButton="Cancel"
        confirmButton={<Button negative>Ban {username}</Button>}
        content={`${username} won't be able to download from you or see your shares, and you won't see their search results.`}
        header={`Ban ${username}?`}
        onCancel={() => setConfirmBan(false)}
        onConfirm={() => {
          setConfirmBan(false);
          run(() => groups.ban({ username }), `Banned ${username}`);
        }}
        open={confirmBan}
        size="tiny"
      />
      <GiftPrivilegesModal
        onClose={() => setGiftOpen(false)}
        open={giftOpen}
        username={username}
      />
    </div>
  );
};

const SelfActions = () => {
  const history = useHistory();

  return (
    <div className="user-profile-actions">
      <Button
        content="Edit Profile"
        icon="edit"
        onClick={() => history.push(`${urlBase}/settings/profile`)}
        primary
        size="small"
      />
    </div>
  );
};

const UserProfile = ({ onBrowse, refreshKey = 0, username }) => {
  const { options = {}, state = {} } = useContext(AppContext) ?? {};
  const history = useHistory();
  const [localRefresh, setLocalRefresh] = useState(0);
  const ownInterests = options.soulseek?.interests;
  const ownLiked = useMemo(
    () => new Set(ownInterests?.liked ?? []),
    [ownInterests],
  );
  const isSelf =
    Boolean(state.user?.username) && state.user.username === username;

  const data = useUserProfile({
    isSelf,
    refreshKey: refreshKey + localRefresh,
    username,
  });

  return (
    <div className="user-profile">
      {isSelf && (
        <Message
          className="user-profile-self-note"
          info
          size="small"
        >
          This is you. Other users see the picture and description below.
        </Message>
      )}
      <ProfileCard
        {...data}
        description={
          isSelf ? options.soulseek?.description : data.info?.description
        }
        interests={isSelf ? ownInterests : data.interests}
        onInterestSelect={(item) =>
          history.push(`${urlBase}/interests?item=${encodeURIComponent(item)}`)
        }
        sharedInterests={isSelf ? undefined : ownLiked}
        username={username}
      />
      {isSelf ? (
        <SelfActions />
      ) : (
        <UserActions
          group={data.group}
          onBrowse={onBrowse}
          onChanged={() => setLocalRefresh((value) => value + 1)}
          username={username}
        />
      )}
    </div>
  );
};

export default UserProfile;
