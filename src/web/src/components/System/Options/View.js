import React from 'react';

const View = ({ options }) => {
  return (
    <div>
      <pre>{JSON.stringify(options, null, 2)}</pre>
    </div>
  );
}

export default View;