import React, { Component } from 'react';
import { Route, Link, Switch } from "react-router-dom";

import * as session from '../lib/session';
import { connect, disconnect } from '../lib/server';

import { createApplicationHubConnection } from '../lib/hubFactory';

import './App.css';
import Search from './Search/Search';
import Browse from './Browse/Browse';
import Users from './Users/Users';
import Transfers from './Transfers/Transfers';
import Chat from './Chat/Chat';
import System from './System/System';
import LoginForm from './LoginForm';

import { 
    Sidebar,
    Segment,
    Menu,
    Icon,
    Modal,
    Header,
    Button
} from 'semantic-ui-react';
import Rooms from './Rooms/Rooms';
import ErrorSegment from './Shared/ErrorSegment';

const initialState = {
    login: {
        initialized: false,
        pending: false,
        error: undefined
    },
    applicationState: {},
    error: false,
};

class App extends Component {
    state = initialState;

    componentDidMount = () => {
        this.init();
    };

    init = async () => {
        const { login } = this.state;

        try {
            const securityEnabled = await session.getSecurityEnabled();
    
            if (!securityEnabled) {
                console.debug('application security is not enabled, per api call')
                session.enablePassthrough()
            }

            await session.check();

            const appHub = createApplicationHubConnection();

            appHub.on('state', (state) => {
                console.debug(state)
                this.setState({ applicationState: state });
            });

            appHub.onreconnecting(() => this.setState({ error: true }));
            appHub.onclose(() => this.setState({ error: true }));
            appHub.onreconnected(() => this.setState({ error: false }));

            appHub.start();

            this.setState({
                login: {
                    ...login,
                    initialized: true
                },
                error: false,
            });
        } catch (err) {
            console.error(err)
            this.setState({ error: true }, () => setTimeout(() => this.init(), 1000))
        }
    }

    login = (username, password, rememberMe) => {
        this.setState({ login: { ...this.state.login, pending: true, error: undefined }}, async () => {
            try {
                await session.login({ username, password, rememberMe });
                window.location.reload();
            } catch (error) {
                this.setState({ login: { ...this.state.login, pending: false, error }});
            }
        });
    };
    
    logout = () => {
        session.logout();
        this.setState({ ...initialState, login: { ...initialState.login, initialized: true }});
    };

    withTokenCheck = (component) => {
        session.check(); // async, runs in the background
        return { ...component };
    };

    render = () => {
        const { login, applicationState = {}, error } = this.state;
        const { version = {}, server } = applicationState;
        const { isUpdateAvailable, current, latest } = version;

        return (
            <>
                {error ? <ErrorSegment caption='slskd appears to be offline' /> : !session.isLoggedIn() && !session.isPassthroughEnabled() ? 
                    <LoginForm 
                        onLoginAttempt={this.login} 
                        initialized={login.initialized}
                        loading={login.pending} 
                        error={login.error}
                    /> : 
                    login.initialized && <Sidebar.Pushable as={Segment} className='app'>
                        <Sidebar 
                            as={Menu} 
                            animation='overlay' 
                            icon='labeled' 
                            inverted 
                            horizontal='true'
                            direction='top' 
                            visible width='thin'
                        >
                            <Link to='.'>
                                <Menu.Item>
                                    <Icon name='search'/>Search
                                </Menu.Item>
                            </Link>
                            <Link to='downloads'>
                                <Menu.Item>
                                    <Icon name='download'/>Downloads
                                </Menu.Item>
                            </Link>
                            <Link to='uploads'>
                                <Menu.Item>
                                    <Icon name='upload'/>Uploads
                                </Menu.Item>
                            </Link>
                            <Link to='rooms'>
                                <Menu.Item>
                                    <Icon name='comments'/>Rooms
                                </Menu.Item>
                            </Link>
                            <Link to='chat'>
                                <Menu.Item>
                                    <Icon name='comment'/>Chat
                                </Menu.Item>
                            </Link>
                            <Link to='users'>
                                <Menu.Item>
                                    <Icon name='users'/>Users
                                </Menu.Item>
                            </Link>
                            <Link to='browse'>
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
                                        <p>You are currently running version <strong>{current}</strong> while version <strong>{latest}</strong> is available.</p>
                                    </Modal.Content>
                                    <Modal.Actions>
                                        <Button
                                            style={{marginLeft: 0}}
                                            primary 
                                            fluid
                                            href="https://github.com/slskd/slskd/releases">See Release Notes</Button>
                                    </Modal.Actions>
                                </Modal>}
                                <Link to='system'>
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
                                    actions={['Cancel', { key: 'done', content: 'Log Out', negative: true, onClick: session.logout }]}
                                />}
                            </Menu>
                        </Sidebar>
                        <Sidebar.Pusher className='app-content'>
                            <Switch>
                                <Route path='*/browse' render={(props) => this.withTokenCheck(<Browse {...props}/>)}/>
                                <Route path='*/users' render={(props) => this.withTokenCheck(<Users {...props}/>)}/>
                                <Route path='*/chat' render={(props) => this.withTokenCheck(<Chat {...props}/>)}/>
                                <Route path='*/rooms' render={(props) => this.withTokenCheck(<Rooms {...props}/>)}/>
                                <Route path='*/uploads' render={(props) => this.withTokenCheck(<Transfers {...props} direction='upload'/>)}/>
                                <Route path='*/downloads' render={(props) => this.withTokenCheck(<Transfers {...props} direction='download'/>)}/>
                                <Route path='*/system' render={(props) => this.withTokenCheck(<System state={this.state.applicationState}/>)}/>
                                <Route path='*/' render={(props) => this.withTokenCheck(<Search {...props}/>)}/>
                            </Switch>
                        </Sidebar.Pusher>
                    </Sidebar.Pushable>
                }
            </>
        )
    };
};

export default App;
