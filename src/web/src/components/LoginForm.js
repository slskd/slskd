import React, { Component } from 'react'
import { Button, Form, Grid, Header, Icon, Segment, Checkbox, Message } from 'semantic-ui-react'

const initialState = {
    username: '',
    password: '',
    rememberMe: true,
    ready: false,
}

class LoginForm extends Component {
    state = initialState;

    handleChange = (field, value) => {
        this.setState({
            [field]: value,
        }, () => {
            if (this.state.username !== '' && this.state.password !== '') {
                this.setState({ ready: true })
            } else {
                this.setState({ ready: false })
            }
        });
    }

    render = () => {
        const { initialized, onLoginAttempt, loading, error } = this.props;
        const { username, password, rememberMe, ready } = this.state;

        return (
            <>
                {initialized && <Grid textAlign='center' style={{ height: '100vh' }} verticalAlign='middle'>
                    <Grid.Column style={{ maxWidth: 372 }}>
                        <Header as='h2' textAlign='center'>
                            slsk<strong>d</strong>
                        </Header>
                        <Form size='large'>
                            <Segment loading={loading}>
                                <Form.Input 
                                    fluid icon='user' 
                                    iconPosition='left' 
                                    placeholder='Username' 
                                    onChange={(event) => this.handleChange('username', event.target.value)}
                                />
                                <Form.Input
                                    fluid
                                    icon='lock'
                                    iconPosition='left'
                                    placeholder='Password'
                                    type='password'
                                    onChange={(event) => this.handleChange('password', event.target.value)}
                                />
                                <Checkbox
                                    label='Remember Me'
                                    onChange={() => this.handleChange('rememberMe', !rememberMe)}
                                    checked={rememberMe}
                                />
                            </Segment>
                            <Button 
                                primary 
                                fluid 
                                size='large'
                                className='login-button'
                                loading={loading}
                                disabled={!ready || loading}
                                onClick={() => onLoginAttempt(username, password, rememberMe)}
                            >
                                <Icon name='sign in'/>
                                Login
                            </Button>
                            {error && <Message className='login-failure' negative floating>
                                <Icon name='x' />
                                {error.message}
                            </Message>}
                        </Form>
                    </Grid.Column>
                </Grid>}
            </>
        )
    }
}

export default LoginForm