import React, { Component } from 'react';
import { Route, Link, Switch } from "react-router-dom";
import { tokenKey, tokenPassthroughValue } from '../config';
import * as session from '../lib/session';
import * as server from '../lib/server';

import './App.css';
import Search from './Search/Search';
import Browse from './Browse/Browse';
import Users from './Users/Users';
import Transfers from './Transfers/Transfers';
import Chat from './Chat/Chat';
import LoginForm from './LoginForm';

import { 
    Sidebar,
    Segment,
    Menu,
    Icon,
    Modal,
    Header
} from 'semantic-ui-react';
import Rooms from './Rooms/Rooms';

const initialState = {
    token: undefined,
    login: {
        initialized: false,
        pending: false,
        error: undefined
    },
    serverStateInterval: undefined,
    serverState: {}
};

class App extends Component {
    state = initialState;

    componentDidMount = async () => {
        const { login } = this.state;

        await this.fetchServerState();
        const securityEnabled = await session.getSecurityEnabled();

        if (!securityEnabled) {
            this.setToken(sessionStorage, tokenPassthroughValue)
        }
        
        this.setState({
            token: this.getToken(),
            login: {
                ...login,
                initialized: true
            },
            serverStateInterval: window.setInterval(this.fetchServerState, 5000)
        });

        await this.checkToken();
    };

    componentWillUnmount = () => {
        clearInterval(this.state.serverStateInterval);
        this.setState({ serverStateInterval: undefined });
    };

    checkToken = async () => {
        try {
            await session.check();
        } catch (error) {
            this.logout();
        }
    };

    getToken = () => JSON.parse(sessionStorage.getItem(tokenKey) || localStorage.getItem(tokenKey));
    setToken = (storage, token) => storage.setItem(tokenKey, JSON.stringify(token));

    login = (username, password, rememberMe) => {
        this.setState({ login: { ...this.state.login, pending: true, error: undefined }}, async () => {
            try {
                const response = await session.login({ username, password });
                this.setToken(rememberMe ? localStorage : sessionStorage, response.data.token);
                window.location.reload();
            } catch (error) {
                this.setState({ login: { ...this.state.login, pending: false, error }});
            }
        });
    };
    
    logout = () => {
        localStorage.removeItem(tokenKey);
        sessionStorage.removeItem(tokenKey);
        this.setState({ ...initialState, login: { ...initialState.login, initialized: true }});
    };

    withTokenCheck = (component) => {
        this.checkToken(); // async, runs in the background
        return { ...component };
    };

    connect = async () => {
        await server.connect();
        this.fetchServerState();
    };
    
    disconnect = async () => {
        await server.disconnect();
        this.fetchServerState();
    }

    fetchServerState = async () => {
        this.setState({ 
            serverState: await server.getState()
        });
    };

    render = () => {
        const { token, login, serverState } = this.state;

        return (
            <>
                {!token ? 
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
                            {serverState.isConnected && <Menu.Item
                                position='right'
                                onClick={() => this.disconnect()}
                            >
                                <Icon name='wifi' color='green'/>Connected
                            </Menu.Item>}
                            {(!serverState.isConnected || serverState.isTransitioning) && <Menu.Item 
                                position='right'
                                onClick={() => this.connect()}
                            >
                                <Icon.Group className='menu-icon-group'>
                                    <Icon name='wifi' color='grey'/>
                                    <Icon name='close' color='red' corner='bottom right' className='menu-icon-no-shadow'/>
                                </Icon.Group>Disconnected
                            </Menu.Item>}
                            {token !== tokenPassthroughValue && <Modal
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
                        </Sidebar>
                        <Sidebar.Pusher className='app-content'>
                            <Switch>
                                <Route path='*/browse' render={(props) => this.withTokenCheck(<Browse {...props}/>)}/>
                                <Route path='*/users' render={(props) => this.withTokenCheck(<Users {...props}/>)}/>
                                <Route path='*/chat' render={(props) => this.withTokenCheck(<Chat {...props}/>)}/>
                                <Route path='*/rooms' render={(props) => this.withTokenCheck(<Rooms {...props}/>)}/>
                                <Route path='*/uploads' render={(props) => this.withTokenCheck(<Transfers {...props} direction='upload'/>)}/>
                                <Route path='*/downloads' render={(props) => this.withTokenCheck(<Transfers {...props} direction='download'/>)}/>
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
