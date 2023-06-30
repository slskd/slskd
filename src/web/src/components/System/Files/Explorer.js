import React, { useState, useEffect } from 'react';

import { list } from '../../../lib/files';

const Explorer = ({ root }) => {
  const [directory, setDirectory] = useState({ files: [], directories: [] });
  const [subdirectory, setSubdirectory] = useState([]);

  useEffect(() => {
    console.log('fetching...', subdirectory);
    const fetch = async () => {
      const dir = await list({ root, subdirectory: subdirectory.join('/') });
      setDirectory(dir);
    };

    fetch();
  }, [subdirectory]);

  const select = ({ path }) => {
    setSubdirectory([...subdirectory, path]);
  };

  return (
    <div>
      {directory?.directories?.map(d => <div>
        <span onClick={() => select({ path: d.name })}>
          {d.name}
        </span></div>)}
    </div>
  );
};

export default Explorer;