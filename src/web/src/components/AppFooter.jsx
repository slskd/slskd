import { formatBytes } from '../lib/util';
import React from 'react';
import { Icon, Menu } from 'semantic-ui-react';

const formatSpeed = (bytesPerSecond) => {
  if (!bytesPerSecond) {
    return '0 B/s';
  }

  return `${formatBytes(bytesPerSecond)}/s`;
};

const AppFooter = ({
  server = {},
  transferMetrics = {},
  user = {},
  version = {},
}) => {
  const { isConnected } = server;
  const { username } = user;
  const { current } = version;
  const downloadSpeed = transferMetrics?.downloads?.inProgress?.totalSpeed;
  const uploadSpeed = transferMetrics?.uploads?.inProgress?.totalSpeed;

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
        <Icon name="arrow up" />
        {formatSpeed(uploadSpeed)}
      </Menu.Item>
      <Menu.Item>
        <Icon name="arrow down" />
        {formatSpeed(downloadSpeed)}
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
