import * as userActions from './userActions';

const formatReply = (message) => {
  if (!message) {
    return '';
  }

  if (typeof message.direction === 'string') {
    return `${message.message} --> `;
  }

  return `[${message.username}] ${message.message} --> `;
};

/**
 * Creates context menu handlers that can be used in class components
 * @param {object} component - The component instance (component)
 * @param {object} options - Configuration options
 * @param {Function} options.getMessageRef - Function that returns the message input ref
 * @param {Function} options.focusInput - Function to focus the input
 * @param {string[]} options.handlerKeys - Optional list of context menu actions to show
 * @returns {object} Object containing all context menu handler methods
 */
export const createContextMenuHandlers = (component, options = {}) => {
  const {
    getMessageRef = () => component.messageRef,
    focusInput = () => component.focusInput(),
    handlerKeys = ['reply', 'userProfile', 'browseShares', 'ignoreUser'],
  } = options;

  const actionMap = {
    browseShares: {
      handlerName: 'handleBrowseShares',
      label: 'Browse Shares',
    },
    ignoreUser: {
      handlerName: 'handleIgnoreUser',
      label: 'Ignore User',
    },
    reply: {
      handlerName: 'handleReply',
      label: 'Reply',
    },
    userProfile: {
      handlerName: 'handleUserProfile',
      label: 'User Profile',
    },
  };

  return {
    getContextMenuActions() {
      return handlerKeys
        .map((key) => {
          const action = actionMap[key];
          if (!action) {
            return null;
          }

          return {
            handleClick: component[action.handlerName],
            label: action.label,
          };
        })
        .filter(Boolean);
    },

    handleBrowseShares() {
      const username = component.state.contextMenu?.message?.username;
      if (!username) return;
      userActions.navigateToBrowseShares(component.props.history, username);
      component.handleCloseContextMenu();
    },

    handleCloseContextMenu() {
      component.setState((previousState) => ({
        contextMenu: {
          ...previousState.contextMenu,
          open: false,
        },
      }));
    },

    handleContextMenu(clickEvent, message) {
      clickEvent.preventDefault();
      component.setState({
        contextMenu: {
          message,
          open: true,
          x: clickEvent.pageX,
          y: clickEvent.pageY,
        },
      });
    },

    async handleIgnoreUser() {
      const username = component.state.contextMenu?.message?.username;
      if (!username) return;
      await userActions.ignoreUser(username);
      component.handleCloseContextMenu();
    },

    handleReply() {
      const message = component.state.contextMenu.message;
      const messageRef = getMessageRef();
      if (messageRef.current && message) {
        messageRef.current.value = formatReply(message);
      }

      focusInput();
      component.handleCloseContextMenu();
    },

    handleUserProfile() {
      const username = component.state.contextMenu?.message?.username;
      if (!username) return;
      userActions.navigateToUserProfile(component.props.history, username);
      component.handleCloseContextMenu();
    },
  };
};

/**
 * Initial state for context menu
 */
export const contextMenuInitialState = {
  message: null,
  open: false,
  x: 0,
  y: 0,
};
