import Logos from './Shared/Logo';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  Button,
  Checkbox,
  Form,
  Grid,
  Header,
  Icon,
  Input,
  Message,
  Segment,
} from 'semantic-ui-react';

const initialState = {
  password: '',
  rememberMe: true,
  username: '',
};

const LoginForm = ({ error, loading, onLoginAttempt }) => {
  const usernameInput = useRef();
  const [state, setState] = useState(initialState);
  const [ready, setReady] = useState(false);
  const logo = useMemo(
    () => Logos[Math.floor(Math.random() * Logos.length)],
    [],
  );

  useEffect(() => {
    if (state.username !== '' && state.password !== '') {
      setReady(true);
    } else {
      setReady(false);
    }
  }, [state]);

  useEffect(() => {
    usernameInput.current?.focus();
  }, [loading]);

  const handleChange = (field, value) => {
    setState({
      ...state,
      [field]: value,
    });
  };

  const { password, rememberMe, username } = state;

  return (
    <Grid
      style={{ height: '100vh' }}
      textAlign="center"
      verticalAlign="middle"
    >
      <Grid.Column style={{ maxWidth: 372 }}>
        <Header
          as="h2"
          style={{
            fontFamily: 'monospace',
            fontSize: 'inherit',
            letterSpacing: -1,
            lineHeight: 1.1,
            whiteSpace: 'pre',
          }}
          textAlign="center"
        >
          {logo}
        </Header>
        <Form size="large">
          <Segment raised>
            <Input
              disabled={loading}
              fluid
              icon="user"
              iconPosition="left"
              onChange={(event) => handleChange('username', event.target.value)}
              placeholder="Username"
              ref={usernameInput}
            />
            <Form.Input
              disabled={loading}
              fluid
              icon="lock"
              iconPosition="left"
              onChange={(event) => handleChange('password', event.target.value)}
              placeholder="Password"
              type="password"
            />
            <Checkbox
              checked={rememberMe}
              disabled={loading}
              label="Remember Me"
              onChange={() => handleChange('rememberMe', !rememberMe)}
            />
          </Segment>
          <Button
            className="login-button"
            disabled={!ready || loading}
            fluid
            loading={loading}
            onClick={() => onLoginAttempt(username, password, rememberMe)}
            primary
            size="large"
          >
            <Icon name="sign in" />
            Login
          </Button>
          {error && (
            <Message
              className="login-failure"
              floating
              negative
            >
              <Icon name="x" />
              {error.message}
            </Message>
          )}
        </Form>
      </Grid.Column>
    </Grid>
  );
};

export default LoginForm;
