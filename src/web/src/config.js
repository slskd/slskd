const rootUrl = process.env.NODE_ENV === 'production' ? '' : 'http://localhost:5000/';
const apiBaseUrl = `${rootUrl}api/v0`;
const tokenKey = 'slskd-token';
const tokenPassthroughValue = 'n/a';
const activeChatKey = 'slskd-active-chat';
const activeRoomKey = 'slskd-active-room';
const activeUserInfoKey = 'slskd-active-user';

// url base is the value of the url base from config;
// the route used when behind a reverse proxy.  we
// need this value so we can be explicit with react-router.
let reactRouterBaseUrl;

const setReactRouterBaseUrl = (url) => {
    reactRouterBaseUrl = (url === '/' ? '' : url);
};

export {
    rootUrl,
    apiBaseUrl,
    reactRouterBaseUrl,
    tokenKey,
    tokenPassthroughValue,
    activeChatKey,
    activeRoomKey,
    activeUserInfoKey,
    setReactRouterBaseUrl,
};
