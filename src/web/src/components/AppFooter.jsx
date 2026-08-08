import { formatBytes } from '../lib/util';
import React from 'react';
import { Icon, Menu } from 'semantic-ui-react';

const formatSpeed = (bytesPerSecond) => {
  if (!bytesPerSecond || bytesPerSecond < 1) {
    return '   0 B/s';
  }

  return `${formatBytes(bytesPerSecond, 1, 4, ' ')}/s`;
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
  // const { downloads, uploads } = transferMetrics ?? {};
  const downloadSpeed = transferMetrics?.downloads?.inProgress?.averageSpeed;
  const downloadActive = transferMetrics?.downloads?.inProgress?.files ?? 0;
  const downloadQueued = transferMetrics?.downloads?.queued?.files ?? 0;
  const uploadSpeed = transferMetrics?.uploads?.inProgress?.averageSpeed;
  const uploadActive = transferMetrics?.uploads?.inProgress?.files ?? 0;
  const uploadQueued = transferMetrics?.uploads?.queued?.files ?? 0;

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
        <Icon
          color="blue"
          name="arrow down"
        />
        <span className="footer-download">{formatSpeed(downloadSpeed)}</span>
        <span className="footer-stat-detail">
          {downloadActive} active &middot; {downloadQueued} queued
        </span>
      </Menu.Item>
      <Menu.Item>
        <Icon
          color="green"
          name="arrow up"
        />
        <span className="footer-upload">{formatSpeed(uploadSpeed)}</span>
        <span className="footer-stat-detail">
          {uploadActive} active &middot; {uploadQueued} queued
        </span>
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
          <span className="footer-license">AGPLv3</span>
        </Menu.Item>
      </Menu.Menu>
    </Menu>
  );
};

export default AppFooter;
