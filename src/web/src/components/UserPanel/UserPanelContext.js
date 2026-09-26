import { createContext, useContext } from 'react';

// lets any component open the user panel, the username context menu, or the
// message composer without prop drilling.  undefined outside the provider
const UserPanelContext = createContext(undefined);

export const useUserPanel = () => useContext(UserPanelContext);

export default UserPanelContext;
