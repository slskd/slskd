import SearchBar from './SearchBar';
import React from 'react';

const Dashboard = ({ server } = {}) => {
  return (
    <div className="dashboard">
      <SearchBar server={server} />
    </div>
  );
};

export default Dashboard;
