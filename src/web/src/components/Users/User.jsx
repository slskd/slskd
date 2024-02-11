import { Icon, Item } from 'semantic-ui-react';

const ImagePlaceholder = () => (
  <div className="users-picture-placeholder ui small image">
    <Icon
      name="camera"
      size="big"
    />
  </div>
);

const Presence = ({ presence }) => {
  const colors = {
    Away: 'yellow',
    Online: 'green',
  };

  return (
    <Icon
      color={colors[presence] || 'grey'}
      name="circle"
    />
  );
};

const FreeUploadSlot = ({ hasFreeUploadSlot }) => (
  <Icon
    color={hasFreeUploadSlot ? 'green' : 'red'}
    name={hasFreeUploadSlot ? 'check' : 'close'}
  />
);

const User = ({
  address,
  description,
  hasPicture,
  picture,
  port,
  presence,
  queueLength,
  uploadSlots,
  username,
}) => (
  <Item>
    {hasPicture ? (
      <Item.Image
        size="small"
        src={`data:image;base64,${picture}`}
      />
    ) : (
      <ImagePlaceholder />
    )}

    <Item.Content>
      <Item.Header as="a">
        <Presence presence={presence} />
        {username}
      </Item.Header>
      <Item.Meta>
        Free Upload Slot: <FreeUploadSlot hasFreeUploadSlot />, Total Upload
        Slots: {uploadSlots}, Queue Length: {queueLength}, IP Address: {address}
        , Port: {port}
      </Item.Meta>
      <Item.Description>{description || 'No user info.'}</Item.Description>
    </Item.Content>
  </Item>
);

export default User;
