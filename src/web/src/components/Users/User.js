import { 
  Item,
  Icon
} from 'semantic-ui-react';

const ImagePlaceholder = () => 
  (<div className='users-picture-placeholder ui small image'><Icon name='camera' size='big'/></div>);

const Presence = ({ presence }) => {
  console.log(presence);
  const colors = {
    Away: 'yellow',
    Online: 'green'
  }

  return <Icon name='circle' color={colors[presence] || 'grey'}/>
};

const FreeUploadSlot = ({ hasFreeUploadSlot }) => (
  <Icon 
    name={hasFreeUploadSlot ? 'check' : 'close'}
    color={hasFreeUploadSlot ? 'green' : 'red'}
  />
);

const User = ({ 
    username, 
    description, 
    hasFreeUploadSlot, 
    hasPicture, 
    picture, 
    queueLength, 
    uploadSlots, 
    updatedAt,
    isPrivileged,
    presence,
    address,
    port,
  }) => (
    <>
      <Item>
        {hasPicture ? 
          <Item.Image size='small' src={`data:image;base64,${picture}`} />
          : <ImagePlaceholder/>}

        <Item.Content>
          <Item.Header as='a'><Presence presence={presence}/>{username}</Item.Header>
          <Item.Meta>
            Free Upload Slot: <FreeUploadSlot hasFreeUploadSlot/>, Total Upload Slots: {uploadSlots}, Queue Length: {queueLength}, IP Address: {address}, Port: {port}
          </Item.Meta>
          <Item.Description>
            {description || 'No user info.'}
          </Item.Description>
        </Item.Content>
      </Item>
    </>
);

export default User;