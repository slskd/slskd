const Switch = ({ children, ...rest }) => {
  const values = Object.values(rest);

  for (const value of values) {
    if (value) return value;
  }

  return children;
};

export default Switch;
