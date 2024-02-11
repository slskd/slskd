import './Chat.css';
import React, { useEffect } from 'react';
import { Button, Form, Header, Icon, Input, Modal } from 'semantic-ui-react';

const usernameRef = React.createRef();

const SendMessageModal = ({ initiateConversation, ...rest }) => {
  const [open, setOpen] = React.useState(false);
  const [username, setUsername] = React.useState('');
  const [message, setMessage] = React.useState('');

  useEffect(() => {
    if (open) {
      usernameRef.current.focus();
    }
  }, [open]);

  const validInput = () => {
    return username.length > 0 && message.length > 0;
  };

  const sendMessage = async () => {
    if (!validInput()) {
      usernameRef.current.focus();
      return;
    }

    await initiateConversation(username, message);
    setOpen(false);
  };

  return (
    <Modal
      onClose={() => setOpen(false)}
      onOpen={() => setOpen(true)}
      open={open}
      {...rest}
    >
      <Header>
        <Icon name="send" />
        <Modal.Content>Send Private Message</Modal.Content>
      </Header>
      <Modal.Content>
        <Form>
          <Form.Field>
            <Input
              onChange={(_event, data) => setUsername(data.value)}
              placeholder="Username"
              ref={usernameRef}
            />
          </Form.Field>
          <Form.Field>
            <Input
              onChange={(_event, data) => setMessage(data.value)}
              placeholder="Message"
            />
          </Form.Field>
        </Form>
      </Modal.Content>
      <Modal.Actions>
        <Button onClick={() => setOpen(false)}>Cancel</Button>
        <Button
          disabled={!validInput()}
          onClick={() => sendMessage()}
          positive
        >
          Send
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

export default SendMessageModal;
