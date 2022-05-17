const Switch = ({ children, ...rest }) => {
  const values = Object.values(rest);

  for (let i = 0; i < values.length; i++) {
    if (values[i]) return values[i];
  }

  return children;
}

export default Switch;