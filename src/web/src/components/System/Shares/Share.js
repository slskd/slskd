import React from 'react';

import {
  Item,
} from 'semantic-ui-react';

const Share = ({ share = {}, onBrowse } = {}) => {
  const { id, remotePath, localPath, mask, alias, isExcluded, directories, files } = share;

  return (
    <Item>
      <Item.Content>
        <Item.Header as='a' onClick={onBrowse}>{remotePath}</Item.Header>
        <Item.Meta>Description</Item.Meta>
        <Item.Description>
          <pre>
            {JSON.stringify({ id, localPath, mask, alias, isExcluded, directories, files }, null, 2)}
          </pre>
        </Item.Description>
      </Item.Content>
    </Item>
  );
};

export default Share;