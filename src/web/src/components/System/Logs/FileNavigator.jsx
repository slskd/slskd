import './Logs.css';
import { formatBytes, formatDate } from '../../../lib/util';
import React from 'react';
import { Button, Dropdown } from 'semantic-ui-react';

/**
 * Sits just above the log display.  The dropdown selects a file directly; the
 * arrow buttons step through the newest-to-oldest sorted file list one at a
 * time, left toward newer files and right toward older ones.
 */
const FileNavigator = ({ file, files, onChange }) => {
  const index = files.findIndex((f) => f.name === file);

  const options = files.map((f) => ({
    description: `${formatBytes(f.length)} · ${formatDate(f.modifiedAt)}`,
    key: f.name,
    text: f.name,
    value: f.name,
  }));

  const goNewer = () => {
    if (index > 0) {
      onChange(files[index - 1].name);
    }
  };

  const goOlder = () => {
    if (index !== -1 && index < files.length - 1) {
      onChange(files[index + 1].name);
    }
  };

  return (
    <div className="logs-file-navigator">
      <Button
        disabled={index <= 0}
        icon="angle left"
        onClick={goNewer}
        title="Newer log"
      />
      <Dropdown
        button
        className="logs-file-navigator-dropdown icon"
        disabled={options.length === 0}
        floating
        fluid
        icon="file outline"
        labeled
        onChange={(_event, { value }) => onChange(value)}
        options={options}
        scrolling
        text={file ?? 'No log files'}
      />
      <Button
        disabled={index === -1 || index >= files.length - 1}
        icon="angle right"
        onClick={goOlder}
        title="Older log"
      />
    </div>
  );
};

export default FileNavigator;
