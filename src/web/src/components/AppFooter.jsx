import React from 'react';
import { Icon, Menu } from 'semantic-ui-react';

const AppFooter = ({ server = {}, user = {}, version = {} }) => {
  const { isConnected } = server;
  const { username } = user;
  const { current } = version;

  return (
    <Menu
      className="footer"
      inverted
    >
      <Menu.Item>
        <Icon
          color={isConnected ? 'green' : 'red'}
          name="circle"
        />
        {isConnected ? username : 'Disconnected'}
      </Menu.Item>
      <Menu.Item>
        <Icon name="arrow up" />—
      </Menu.Item>
      <Menu.Item>
        <Icon name="arrow down" />—
      </Menu.Item>
      <Menu.Menu position="right">
        <Menu.Item
          as="a"
          href="https://github.com/slskd/slskd"
          rel="noreferrer"
          target="_blank"
        >
          <img
            alt=""
            className="footer-logo"
            src="/favicon.ico"
          />
          slskd
          {current && <span className="footer-version">{current}</span>}
          <span className="footer-license">AGPL-3.0</span>
        </Menu.Item>
      </Menu.Menu>
    </Menu>
  );
};

export default AppFooter;
