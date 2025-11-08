import * as userActions from './userActions';

/**
 * Creates context menu handlers that can be used in class components
 * @param {object} component - The component instance (this)
 * @param {object} options - Configuration options
 * @param {Function} options.getMessageRef - Function that returns the message input ref
 * @param {Function} options.focusInput - Function to focus the input
 * @param {Function} options.formatReply - Optional function to format reply text
 * @returns {object} Object containing all context menu handler methods
 */
export const createContextMenuHandlers = (component, options = {}) => {
  const {
    getMessageRef = () => component.messageRef,
    focusInput = () => component.focusInput(),
    formatReply = (message) => `${message.message} --> `,
  } = options;

  return {
    getContextMenuActions() {
      return [
        {
          handleClick: this.handleReply,
          label: 'Reply',
        },
        {
          handleClick: this.handleUserProfile,
          label: 'User Profile',
        },
        {
          handleClick: this.handleBrowseShares,
          label: 'Browse Shares',
        },
        {
          handleClick: this.handleIgnoreUser,
          label: 'Ignore User',
        },
      ];
    },

    handleBrowseShares() {
      const username = component.state.contextMenu?.message?.username;
      if (!username) return;
      userActions.navigateToBrowseShares(component.props.history, username);
      this.handleCloseContextMenu();
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
      this.handleCloseContextMenu();
    },

    handleReply() {
      const message = component.state.contextMenu.message;
      const messageRef = getMessageRef();
      if (messageRef.current && message) {
        messageRef.current.value = formatReply(message);
      }

      focusInput();
      this.handleCloseContextMenu();
    },

    handleUserProfile() {
      const username = component.state.contextMenu?.message?.username;
      if (!username) return;
      userActions.navigateToUserProfile(component.props.history, username);
      this.handleCloseContextMenu();
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
