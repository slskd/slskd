import 'react-toastify/dist/ReactToastify.css';
import './App.css';
import { urlBase } from '../config';
import { createApplicationHubConnection } from '../lib/hubFactory';
import * as relayAPI from '../lib/relay';
import { connect, disconnect } from '../lib/server';
import * as session from '../lib/session';
import { isPassthroughEnabled } from '../lib/token';
import AppContext from './AppContext';
import Browse from './Browse/Browse';
import Chat from './Chat/Chat';
import LoginForm from './LoginForm';
import Rooms from './Rooms/Rooms';
import Searches from './Search/Searches';
import ErrorSegment from './Shared/ErrorSegment';
import System from './System/System';
import Transfers from './Transfers/Transfers';
import Users from './Users/Users';
import React, { Component } from 'react';
import { Link, Redirect, Route, Switch } from 'react-router-dom';
import { ToastContainer } from 'react-toastify';
import {
  Button,
  Header,
  Icon,
  Loader,
  Menu,
  Modal,
  Segment,
  Sidebar,
} from 'semantic-ui-react';

const MOBILE_BREAKPOINT = 767;
const mobileQuery = () =>
  window.matchMedia(`(max-width: ${MOBILE_BREAKPOINT}px)`);

const initialState = {
  applicationOptions: {},
  applicationState: {},
  error: false,
  initialized: false,
  isMobile: typeof window !== 'undefined' && mobileQuery().matches,
  login: {
    error: undefined,
    pending: false,
  },
  retriesExhausted: false,
  sidebarVisible: false,
};

const ModeSpecificConnectButton = ({
  connectionWatchdog,
  controller = {},
  mode,
  pendingReconnect,
  server,
  user,
}) => {
  if (mode === 'Agent') {
    const isConnected = controller?.state === 'Connected';
    const isTransitioning = ['Connecting', 'Reconnecting'].includes(
      controller?.state,
    );

    return (
      <Menu.Item
        onClick={() =>
          isConnected ? relayAPI.disconnect() : relayAPI.connect()
        }
      >
        <Icon.Group className="menu-icon-group">
          <Icon
            color={
              controller?.state === 'Connected'
                ? 'green'
                : isTransitioning
                  ? 'yellow'
                  : 'grey'
            }
            name="plug"
          />
          {!isConnected && (
            <Icon
              className="menu-icon-no-shadow"
              color="red"
              corner="bottom right"
              name="close"
            />
          )}
        </Icon.Group>
        Controller {controller?.state}
      </Menu.Item>
    );
  } else {
    if (server?.isConnected) {
      return (
        <Menu.Item onClick={() => disconnect()}>
          <Icon.Group className="menu-icon-group">
            <Icon
              color={pendingReconnect ? 'yellow' : 'green'}
              name="plug"
            />
            {user?.privileges?.isPrivileged && (
              <Icon
                className="menu-icon-no-shadow"
                color="yellow"
                corner
                name="star"
              />
            )}
          </Icon.Group>
          Connected
        </Menu.Item>
      );
    }

    // the server is disconnected, and we need to give the user some information about what the client is doing
    // options are:
    // - nothing. the client was manually disconnected, kicked off by another login, etc., and we're not trying to connect
    // - actively trying to make a connection to the server
    // - still trying to connect, but waiting for the next connection attempt
    let icon = 'close';
    let color = 'red';

    if (connectionWatchdog?.isAttemptingConnection) {
      icon = 'clock';
      color = 'yellow';
    }

    if (server?.isConnecting || server?.IsLoggingIn) {
      icon = 'sync alternate loading';
      color = 'green';
    }

    return (
      <Menu.Item onClick={() => connect()}>
        <Icon.Group className="menu-icon-group">
          <Icon
            color="grey"
            name="plug"
          />
          <Icon
            className="menu-icon-no-shadow"
            color={color}
            corner="bottom right"
            name={icon}
          />
        </Icon.Group>
        Disconnected
      </Menu.Item>
    );
  }
};

class App extends Component {
  constructor(props) {
    super(props);

    this.state = initialState;
  }

  componentDidMount() {
    if (this.getSavedTheme() == null) {
      window
        .matchMedia('(prefers-color-scheme: dark)')
        .addEventListener(
          'change',
          (event) => event.matches && this.setState({ theme: 'dark' }),
        );
      window
        .matchMedia('(prefers-color-scheme: light)')
        .addEventListener(
          'change',
          (event) => event.matches && this.setState({ theme: 'light' }),
        );
    }

    const mql = mobileQuery();
    this.mobileMediaListener = () => this.setState({ isMobile: mql.matches });
    mql.addEventListener('change', this.mobileMediaListener);

    this.init();
  }

  componentWillUnmount() {
    const mql = mobileQuery();
    if (this.mobileMediaListener) {
      mql.removeEventListener('change', this.mobileMediaListener);
    }
  }

  init = async () => {
    this.setState({ initialized: false }, async () => {
      try {
        const securityEnabled = await session.getSecurityEnabled();

        if (!securityEnabled) {
          console.debug('application security is not enabled, per api call');
          session.enablePassthrough();
        }

        if (await session.check()) {
          const appHub = createApplicationHubConnection();

          appHub.on('state', (state) => {
            this.setState({ applicationState: state });
          });

          appHub.on('options', (options) => {
            this.setState({ applicationOptions: options });
          });

          appHub.onreconnecting(() =>
            this.setState({ error: true, retriesExhausted: false }),
          );
          appHub.onclose(() =>
            this.setState({ error: true, retriesExhausted: true }),
          );
          appHub.onreconnected(() =>
            this.setState({ error: false, retriesExhausted: false }),
          );

          await appHub.start();
        }

        const savedTheme = this.getSavedTheme();
        if (savedTheme != null) {
          this.setState({ theme: savedTheme });
        }

        this.setState({
          error: false,
        });
      } catch (error) {
        console.error(error);
        this.setState({ error: true, retriesExhausted: true });
      } finally {
        this.setState({ initialized: true });
      }
    });
  };

  getSavedTheme = () => {
    return localStorage.getItem('slskd-theme');
  };

  toggleTheme = () => {
    this.setState((state) => {
      const newTheme = state.theme === 'dark' ? 'light' : 'dark';
      localStorage.setItem('slskd-theme', newTheme);
      return { theme: newTheme };
    });
  };

  handleLogin = (username, password, rememberMe) => {
    this.setState(
      (previousState) => ({
        login: { ...previousState.login, error: undefined, pending: true },
      }),
      async () => {
        try {
          await session.login({ password, rememberMe, username });
          this.setState(
            (previousState) => ({
              login: { ...previousState.login, error: false, pending: false },
            }),
            () => this.init(),
          );
        } catch (error) {
          this.setState((previousState) => ({
            login: { ...previousState.login, error, pending: false },
          }));
        }
      },
    );
  };

  logout = () => {
    session.logout();
    this.setState({ login: { ...initialState.login } });
  };

  withTokenCheck = (component) => {
    session.check(); // async, runs in the background
    return { ...component };
  };

  closeSidebar = () => {
    this.setState({ sidebarVisible: false });
  };

  handleToggleSidebar = () => {
    this.setState((s) => ({ sidebarVisible: !s.sidebarVisible }));
  };

  renderMobileLayout = ({
    applicationOptions,
    applicationState,
    mainNav,
    rightMenu,
    sidebarVisible,
    theme,
  }) => {
    const isAgent = applicationState?.relay?.mode === 'Agent';
    const { version = {} } = applicationState;

    return (
      <>
        <Sidebar.Pushable
          as={Segment}
          className="app"
        >
          <Sidebar
            animation="overlay"
            as={Menu}
            className="navigation-mobile-drawer"
            direction="left"
            icon="labeled"
            inverted
            vertical
            visible={sidebarVisible}
            width="wide"
          >
            {version.isCanary && (
              <Menu.Item>
                <Icon
                  color="yellow"
                  name="flask"
                />
                Canary
              </Menu.Item>
            )}
            {mainNav}
            {rightMenu}
          </Sidebar>
          <Sidebar.Pusher
            className="app-content"
            onClick={() => sidebarVisible && this.closeSidebar()}
          >
            <Menu
              className="navigation navigation-mobile"
              fixed="top"
              icon="labeled"
              inverted
            >
              <Menu.Item onClick={this.handleToggleSidebar}>
                <Icon name="sidebar" />
                Menu
              </Menu.Item>
              <Menu.Menu position="right">
                <ModeSpecificConnectButton
                  connectionWatchdog={applicationState?.connectionWatchdog}
                  controller={applicationState?.relay?.controller}
                  mode={applicationState?.relay?.mode}
                  pendingReconnect={applicationState?.pendingReconnect}
                  server={applicationState?.server}
                  user={applicationState?.user}
                />
              </Menu.Menu>
            </Menu>
            <div
              className="app-content-inner"
              style={{ paddingTop: 52 }}
            >
              <AppContext.Provider
                // eslint-disable-next-line react/jsx-no-constructed-context-values
                value={{
                  options: applicationOptions,
                  state: applicationState,
                }}
              >
                {isAgent ? (
                  <Switch>
                    <Route
                      path={`${urlBase}/system/:tab?`}
                      render={(props) =>
                        this.withTokenCheck(
                          <System
                            {...props}
                            options={applicationOptions}
                            state={applicationState}
                          />,
                        )
                      }
                    />
                    <Redirect
                      from="*"
                      to={`${urlBase}/system`}
                    />
                  </Switch>
                ) : (
                  <Switch>
                    <Route
                      path={`${urlBase}/searches/:id?`}
                      render={(props) =>
                        this.withTokenCheck(
                          <div className="view">
                            <Searches
                              server={applicationState.server}
                              {...props}
                            />
                          </div>,
                        )
                      }
                    />
                    <Route
                      path={`${urlBase}/browse`}
                      render={(props) =>
                        this.withTokenCheck(<Browse {...props} />)
                      }
                    />
                    <Route
                      path={`${urlBase}/users`}
                      render={(props) =>
                        this.withTokenCheck(<Users {...props} />)
                      }
                    />
                    <Route
                      path={`${urlBase}/chat`}
                      render={(props) =>
                        this.withTokenCheck(
                          <Chat
                            {...props}
                            state={applicationState}
                          />,
                        )
                      }
                    />
                    <Route
                      path={`${urlBase}/rooms`}
                      render={(props) =>
                        this.withTokenCheck(<Rooms {...props} />)
                      }
                    />
                    <Route
                      path={`${urlBase}/uploads`}
                      render={(props) =>
                        this.withTokenCheck(
                          <div className="view">
                            <Transfers
                              {...props}
                              direction="upload"
                            />
                          </div>,
                        )
                      }
                    />
                    <Route
                      path={`${urlBase}/downloads`}
                      render={(props) =>
                        this.withTokenCheck(
                          <div className="view">
                            <Transfers
                              {...props}
                              direction="download"
                              server={applicationState.server}
                            />
                          </div>,
                        )
                      }
                    />
                    <Route
                      path={`${urlBase}/system/:tab?`}
                      render={(props) =>
                        this.withTokenCheck(
                          <System
                            {...props}
                            options={applicationOptions}
                            state={applicationState}
                            theme={theme}
                          />,
                        )
                      }
                    />
                    <Redirect
                      from="*"
                      to={`${urlBase}/searches`}
                    />
                  </Switch>
                )}
              </AppContext.Provider>
            </div>
          </Sidebar.Pusher>
        </Sidebar.Pushable>
        <ToastContainer
          autoClose={5_000}
          closeOnClick
          draggable={false}
          hideProgressBar={false}
          newestOnTop
          pauseOnFocusLoss
          pauseOnHover
          position="bottom-center"
          rtl={false}
        />
      </>
    );
  };

  render() {
    const {
      applicationOptions = {},
      applicationState = {},
      error,
      initialized,
      isMobile,
      login,
      retriesExhausted,
      sidebarVisible,
      theme = this.getSavedTheme() ||
        (window.matchMedia('(prefers-color-scheme: dark)').matches
          ? 'dark'
          : 'light'),
    } = this.state;
    const {
      connectionWatchdog = {},
      pendingReconnect,
      pendingRestart,
      relay = {},
      server,
      shares = {},
      user,
      version = {},
    } = applicationState;
    const { current, isUpdateAvailable, latest } = version;
    const { scanPending: pendingShareRescan } = shares;

    const { controller, mode } = relay;

    if (!initialized) {
      return (
        <Loader
          active
          size="big"
        />
      );
    }

    if (error) {
      return (
        <ErrorSegment
          caption={
            <>
              <span>Lost connection to slskd</span>
              <br />
              <span>
                {retriesExhausted ? 'Refresh to reconnect' : 'Retrying...'}
              </span>
            </>
          }
          icon="attention"
          suppressPrefix
        />
      );
    }

    if (!session.isLoggedIn() && !isPassthroughEnabled()) {
      return (
        <LoginForm
          error={login.error}
          initialized={login.initialized}
          loading={login.pending}
          onLoginAttempt={this.handleLogin}
        />
      );
    }

    const isAgent = mode === 'Agent';

    if (theme === 'dark') {
      document.documentElement.classList.add(theme);
    } else {
      document.documentElement.classList.remove('dark');
    }

    const closeSidebar = this.closeSidebar;
    const navLink = (to, iconName, label) => (
      <Link
        key={to}
        onClick={isMobile ? closeSidebar : undefined}
        to={to}
      >
        <Menu.Item>
          <Icon name={iconName} />
          {label}
        </Menu.Item>
      </Link>
    );

    const mainNav = isAgent ? (
      <Menu.Item>
        <Icon name="detective" />
        Agent Mode
      </Menu.Item>
    ) : (
      <>
        {navLink(`${urlBase}/searches`, 'search', 'Search')}
        {navLink(`${urlBase}/downloads`, 'download', 'Downloads')}
        {navLink(`${urlBase}/uploads`, 'upload', 'Uploads')}
        {navLink(`${urlBase}/rooms`, 'comments', 'Rooms')}
        {navLink(`${urlBase}/chat`, 'comment', 'Chat')}
        {navLink(`${urlBase}/users`, 'users', 'Users')}
        {navLink(`${urlBase}/browse`, 'folder open', 'Browse')}
      </>
    );

    const rightMenu = (
      <>
        <Menu.Item onClick={() => this.toggleTheme()}>
          <Icon name="theme" />
          Theme
        </Menu.Item>
        <ModeSpecificConnectButton
          connectionWatchdog={connectionWatchdog}
          controller={controller}
          mode={mode}
          pendingReconnect={pendingReconnect}
          server={server}
          user={user}
        />
        {(pendingReconnect || pendingRestart || pendingShareRescan) && (
          <Menu.Item>
            <Icon.Group className="menu-icon-group">
              <Link
                onClick={isMobile ? closeSidebar : undefined}
                to={`${urlBase}/system/info`}
              >
                <Icon
                  color="yellow"
                  name="exclamation circle"
                />
              </Link>
            </Icon.Group>
            Pending Action
          </Menu.Item>
        )}
        {isUpdateAvailable && (
          <Modal
            centered
            closeIcon
            size="mini"
            trigger={
              <Menu.Item>
                <Icon.Group className="menu-icon-group">
                  <Icon
                    color="yellow"
                    name="bullhorn"
                  />
                </Icon.Group>
                New Version!
              </Menu.Item>
            }
          >
            <Modal.Header>New Version!</Modal.Header>
            <Modal.Content>
              <p>
                You are currently running version <strong>{current}</strong>{' '}
                while version <strong>{latest}</strong> is available.
              </p>
            </Modal.Content>
            <Modal.Actions>
              <Button
                fluid
                href="https://github.com/slskd/slskd/releases"
                primary
                style={{ marginLeft: 0 }}
              >
                See Release Notes
              </Button>
            </Modal.Actions>
          </Modal>
        )}
        <Link
          onClick={isMobile ? closeSidebar : undefined}
          to={`${urlBase}/system`}
        >
          <Menu.Item>
            <Icon name="cogs" />
            System
          </Menu.Item>
        </Link>
        {session.isLoggedIn() && (
          <Modal
            actions={[
              'Cancel',
              {
                content: 'Log Out',
                key: 'done',
                negative: true,
                onClick: this.logout,
              },
            ]}
            centered
            content="Are you sure you want to log out?"
            header={
              <Header
                content="Confirm Log Out"
                icon="sign-out"
              />
            }
            size="mini"
            trigger={
              <Menu.Item>
                <Icon name="sign-out" />
                Log Out
              </Menu.Item>
            }
          />
        )}
      </>
    );

    if (isMobile) {
      return this.renderMobileLayout({
        applicationOptions,
        applicationState,
        mainNav,
        rightMenu,
        sidebarVisible,
        theme,
      });
    }

    return (
      <>
        <Sidebar.Pushable
          as={Segment}
          className="app"
        >
          <Sidebar
            animation="overlay"
            as={Menu}
            className="navigation"
            direction="top"
            horizontal="true"
            icon="labeled"
            inverted
            visible
            width="thin"
          >
            {version.isCanary && (
              <Menu.Item>
                <Icon
                  color="yellow"
                  name="flask"
                />
                Canary
              </Menu.Item>
            )}
            {mainNav}
            <Menu
              className="right"
              inverted
            >
              {rightMenu}
            </Menu>
          </Sidebar>
          <Sidebar.Pusher className="app-content">
            <AppContext.Provider
              // eslint-disable-next-line no-warning-comments
              // TODO: needs useMemo, but class component. yolo for now.
              // eslint-disable-next-line react/jsx-no-constructed-context-values
              value={{ options: applicationOptions, state: applicationState }}
            >
              {isAgent ? (
                <Switch>
                  <Route
                    path={`${urlBase}/system/:tab?`}
                    render={(props) =>
                      this.withTokenCheck(
                        <System
                          {...props}
                          options={applicationOptions}
                          state={applicationState}
                        />,
                      )
                    }
                  />
                  <Redirect
                    from="*"
                    to={`${urlBase}/system`}
                  />
                </Switch>
              ) : (
                <Switch>
                  <Route
                    path={`${urlBase}/searches/:id?`}
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <Searches
                            server={applicationState.server}
                            {...props}
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path={`${urlBase}/browse`}
                    render={(props) =>
                      this.withTokenCheck(<Browse {...props} />)
                    }
                  />
                  <Route
                    path={`${urlBase}/users`}
                    render={(props) =>
                      this.withTokenCheck(<Users {...props} />)
                    }
                  />
                  <Route
                    path={`${urlBase}/chat`}
                    render={(props) =>
                      this.withTokenCheck(
                        <Chat
                          {...props}
                          state={applicationState}
                        />,
                      )
                    }
                  />
                  <Route
                    path={`${urlBase}/rooms`}
                    render={(props) =>
                      this.withTokenCheck(<Rooms {...props} />)
                    }
                  />
                  <Route
                    path={`${urlBase}/uploads`}
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <Transfers
                            {...props}
                            direction="upload"
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path={`${urlBase}/downloads`}
                    render={(props) =>
                      this.withTokenCheck(
                        <div className="view">
                          <Transfers
                            {...props}
                            direction="download"
                            server={applicationState.server}
                          />
                        </div>,
                      )
                    }
                  />
                  <Route
                    path={`${urlBase}/system/:tab?`}
                    render={(props) =>
                      this.withTokenCheck(
                        <System
                          {...props}
                          options={applicationOptions}
                          state={applicationState}
                          theme={theme}
                        />,
                      )
                    }
                  />
                  <Redirect
                    from="*"
                    to={`${urlBase}/searches`}
                  />
                </Switch>
              )}
            </AppContext.Provider>
          </Sidebar.Pusher>
        </Sidebar.Pushable>
        <ToastContainer
          autoClose={5_000}
          closeOnClick
          draggable={false}
          hideProgressBar={false}
          newestOnTop
          pauseOnFocusLoss
          pauseOnHover
          position="bottom-center"
          rtl={false}
        />
      </>
    );
  }
}

export default App;
