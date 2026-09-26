import { activeChatKey, urlBase } from '../../config';
import * as chat from '../../lib/chat';
import { getErrorMessage } from '../Users/UserProfile';
import React, { useEffect, useRef, useState } from 'react';
import { useHistory } from 'react-router-dom';
import { toast } from 'react-toastify';
import { Button, Form, Icon, Message, Modal } from 'semantic-ui-react';

const ComposeMessageModal = ({ onClose, username }) => {
  const history = useHistory();
  const [message, setMessage] = useState('');
  const [pending, setPending] = useState(false);
  const [error, setError] = useState();
  // keep showing the name while the modal fades out after closing
  const lastUsername = useRef(username);
  if (username) {
    lastUsername.current = username;
  }

  const displayName = username ?? lastUsername.current;

  useEffect(() => {
    setMessage('');
    setError(undefined);
  }, [username]);

  const openChat = () => {
    sessionStorage.setItem(activeChatKey, username);
    history.push(`${urlBase}/chat`, { user: username });
  };

  const send = async () => {
    if (!message.trim()) {
      return;
    }

    setPending(true);
    setError(undefined);

    try {
      await chat.send({ message, username });
      onClose();
      toast.success(
        <span>
          Message sent to {username}.{' '}
          <button
            className="user-link"
            onClick={openChat}
            type="button"
          >
            Open chat
          </button>
        </span>,
      );
    } catch (sendError) {
      setError(getErrorMessage(sendError));
    } finally {
      setPending(false);
    }
  };

  return (
    <Modal
      onClose={onClose}
      open={Boolean(username)}
      size="small"
    >
      <Modal.Header>
        <Icon name="comment" />
        Message {displayName}
      </Modal.Header>
      <Modal.Content>
        <Form
          error={Boolean(error)}
          onSubmit={send}
        >
          <Form.TextArea
            autoFocus
            label="Message"
            onChange={(_event, { value }) => setMessage(value)}
            onKeyDown={(event) => {
              // enter sends, shift+enter adds a line, like most chat apps
              // (but not while an input method is composing text)
              if (
                event.key === 'Enter' &&
                !event.shiftKey &&
                !event.nativeEvent.isComposing
              ) {
                event.preventDefault();
                send();
              }
            }}
            placeholder={`Say something to ${displayName}`}
            rows={4}
            value={message}
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
          content="Send"
          disabled={!message.trim() || pending}
          icon="send"
          loading={pending}
          onClick={send}
          primary
        />
      </Modal.Actions>
    </Modal>
  );
};

export default ComposeMessageModal;
