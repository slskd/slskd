import React, { Component } from 'react';
import { Route, Link, Switch, Redirect } from 'react-router-dom';

import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';

import * as session from '../lib/session';
import { connect, disconnect } from '../lib/server';
import { urlBase } from '../config';

import { createApplicationHubConnection } from '../lib/hubFactory';

import './App.css';
import Searches from './Search/Searches';
import Browse from './Browse/Browse';
import Users from './Users/Users';
import Transfers from './Transfers/Transfers';
import Chat from './Chat/Chat';
import System from './System/System';
import LoginForm from './LoginForm';

import AppContext from './AppContext';

import { 
  Sidebar,
  Segment,
  Menu,
  Icon,
  Modal,
  Header,
  Button,
  Loader,
} from 'semantic-ui-react';
import Rooms from './Rooms/Rooms';
import ErrorSegment from './Shared/ErrorSegment';

const initialState = {
  login: {
    pending: false,
    error: undefined,
  },
  applicationState: {},
  applicationOptions: {},
  initialized: false,
  error: false,
  retriesExhausted: false,
};

class App extends Component {
  state = initialState;

  componentDidMount = () => {
    this.init();
  };

  init = async () => {
    this.setState({ initialized: false }, async () => {
      try {
        const securityEnabled = await session.getSecurityEnabled();
  
        if (!securityEnabled) {
          console.debug('application security is not enabled, per api call')
          session.enablePassthrough()
        }
  
        if (await session.check()) {
          const appHub = createApplicationHubConnection();

          appHub.on('state', (state) => {
            this.setState({ applicationState: state });
          });
  
          appHub.on('options', (options) => {
            this.setState({ applicationOptions: options })
          })

          appHub.onreconnecting(() => this.setState({ error: true, retriesExhausted: false }));
          appHub.onclose(() => this.setState({ error: true, retriesExhausted: true }));
          appHub.onreconnected(() => this.setState({ error: false, retriesExhausted: false }));

          await appHub.start();
        }
  
        this.setState({
          error: false,
        });
      } catch (err) {
        console.error(err)
        this.setState({ error: true, retriesExhausted: true })
      } finally {
        this.setState({ initialized: true });
      }
    })
  }

  login = (username, password, rememberMe) => {
    this.setState({ login: { ...this.state.login, pending: true, error: undefined }}, async () => {
      try {
        await session.login({ username, password, rememberMe });
        this.setState({ login: { ...this.state.login, pending: false, error: false }}, () => this.init());
      } catch (error) {
        this.setState({ login: { ...this.state.login, pending: false, error }});
      }
    });
  };
  
  logout = () => {
    session.logout();
    this.setState({ login: { ...initialState.login }});
  };

  withTokenCheck = (component) => {
    session.check(); // async, runs in the background
    return { ...component };
  };

  render = () => {
    const { login, applicationState = {}, applicationOptions = {}, error, initialized, retriesExhausted } = this.state;
    const { version = {}, server } = applicationState;
    const { isUpdateAvailable, current, latest } = version;

    if (!initialized) {
      return <Loader active size='big'/>;
    }

    if (error) {
      return <ErrorSegment 
        suppressPrefix 
        icon='attention' 
        caption={
          <>
            <span>
            Lost connection to slskd</span><br /><span>{(retriesExhausted ? 'Refresh to reconnect' : 'Retrying...')}
            </span>
          </>
        }/>;
    }

    if (!session.isLoggedIn() && !session.isPassthroughEnabled()) {
      return (
        <LoginForm 
          onLoginAttempt={this.login} 
          initialized={login.initialized}
          loading={login.pending} 
          error={login.error}
        />
      );
    }

    return (
      <>
        <Sidebar.Pushable as={Segment} className='app'>
          <Sidebar
            className='navigation'
            as={Menu} 
            animation='overlay' 
            icon='labeled' 
            inverted 
            horizontal='true'
            direction='top' 
            visible width='thin'
          >
            {version.isCanary && <Menu.Item>
              <Icon name='flask' color='yellow'/>Canary
            </Menu.Item>}
            <Link to={`${urlBase}/searches`}>
              <Menu.Item>
                <Icon name='search'/>Search
              </Menu.Item>
            </Link>
            <Link to={`${urlBase}/downloads`}>
              <Menu.Item>
                <Icon name='download'/>Downloads
              </Menu.Item>
            </Link>
            <Link to={`${urlBase}/uploads`}>
              <Menu.Item>
                <Icon name='upload'/>Uploads
              </Menu.Item>
            </Link>
            <Link to={`${urlBase}/rooms`}>
              <Menu.Item>
                <Icon name='comments'/>Rooms
              </Menu.Item>
            </Link>
            <Link to={`${urlBase}/chat`}>
              <Menu.Item>
                <Icon name='comment'/>Chat
              </Menu.Item>
            </Link>
            <Link to={`${urlBase}/users`}>
              <Menu.Item>
                <Icon name='users'/>Users
              </Menu.Item>
            </Link>
            <Link to={`${urlBase}/browse`}>
              <Menu.Item>
                <Icon name='folder open'/>Browse
              </Menu.Item>
            </Link>
            <Menu className='right' inverted>
              {server?.isConnected && <Menu.Item
                onClick={() => disconnect()}
              >
                <Icon name='plug' color='green'/>Connected
              </Menu.Item>}
              {(!server?.isConnected) && <Menu.Item 
                onClick={() => connect()}
              >
                <Icon.Group className='menu-icon-group'>
                  <Icon name='plug' color='grey'/>
                  <Icon name='close' color='red' corner='bottom right' className='menu-icon-no-shadow'/>
                </Icon.Group>Disconnected
              </Menu.Item>}
              {isUpdateAvailable && <Modal
                trigger={<Menu.Item position='right'>
                  <Icon.Group className='menu-icon-group'>
                    <Icon name='bullhorn' color='yellow'/>
                  </Icon.Group>New Version!
                </Menu.Item>}
                centered
                closeIcon
                size='mini'
              >
                <Modal.Header>New Version!</Modal.Header>
                <Modal.Content>
                  <p>
                    You are currently running version <strong>{current}</strong>
                    while version <strong>{latest}</strong> is available.
                  </p>
                </Modal.Content>
                <Modal.Actions>
                  <Button
                    style={{marginLeft: 0}}
                    primary 
                    fluid
                    href="https://github.com/slskd/slskd/releases">See Release Notes</Button>
                </Modal.Actions>
              </Modal>}
              <Link to={`${urlBase}/system`}>
                <Menu.Item>
                  <Icon name='cogs'/>System
                </Menu.Item>
              </Link>
              {session.isLoggedIn() && <Modal
                trigger={
                  <Menu.Item>
                    <Icon name='sign-out'/>Log Out
                  </Menu.Item>
                }
                centered
                size='mini'
                header={<Header icon='sign-out' content='Confirm Log Out' />}
                content='Are you sure you want to log out?'
                actions={['Cancel', { key: 'done', content: 'Log Out', negative: true, onClick: this.logout }]}
              />}
            </Menu>
          </Sidebar>
          <Sidebar.Pusher className='app-content'>
            <AppContext.Provider value={{ state: applicationState, options: applicationOptions }}>
              <Switch>
                <Route path={`${urlBase}/searches/:id?`} render={(props) => 
                  this.withTokenCheck(<div className='search-container'>
                    <Searches
                      server={applicationState.server}
                      {...props}
                    />
                  </div>)}
                />
                <Route path={`${urlBase}/browse`} render={(props) => this.withTokenCheck(<Browse {...props}/>)}/>
                <Route path={`${urlBase}/users`} render={(props) => this.withTokenCheck(<Users {...props}/>)}/>
                <Route path={`${urlBase}/chat`} render={(props) => this.withTokenCheck(<Chat {...props}/>)}/>
                <Route path={`${urlBase}/rooms`} render={(props) => this.withTokenCheck(<Rooms {...props}/>)}/>
                <Route path={`${urlBase}/uploads`} render={
                  (props) => this.withTokenCheck(<Transfers {...props} direction='upload' />)
                }/>
                <Route path={`${urlBase}/downloads`} render={
                  (props) => this.withTokenCheck(<Transfers {...props} direction='download' />)
                }/>
                <Route path={`${urlBase}/system/:tab?`} render={
                  (props) => this.withTokenCheck(
                    <System {...props} state={applicationState} options={applicationOptions} />
                  )
                }/>
                <Redirect from='*' to={`${urlBase}/searches`}/>
              </Switch>
            </AppContext.Provider>
          </Sidebar.Pusher>
        </Sidebar.Pushable>
        <ToastContainer
          position="bottom-center"
          autoClose={5000}
          hideProgressBar={false}
          newestOnTop
          closeOnClick
          rtl={false}
          pauseOnFocusLoss
          draggable={false}
          pauseOnHover
        />
      </>
    )
  };
}

export default App;