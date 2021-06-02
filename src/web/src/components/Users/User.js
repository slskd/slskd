import { 
  Item,
  Icon,
  Divider,
  Button
} from 'semantic-ui-react';

const ImagePlaceholder = () => 
  (<div className='users-picture-placeholder ui small image'><Icon name='camera' size='big'/></div>);

const User = ({ 
    username, 
    description, 
    hasFreeUploadSlot, 
    hasPicture, 
    picture, 
    queueLength, 
    uploadSlots, 
    updatedAt,
    fetching
  }) => (
    <>
      <Item>
        {hasPicture ? 
          <Item.Image size='small' src={`data:image;base64,${picture}`} />
          : <ImagePlaceholder/>}

        <Item.Content>
          <Item.Header as='a'>{username}</Item.Header>
          <Item.Meta>
          Free Upload Slot: <Icon 
              name={hasFreeUploadSlot ? 'check' : 'close'}
              color={hasFreeUploadSlot ? 'green' : 'red'}
            />, Total Upload Slots: {uploadSlots}, Queue Length: {queueLength}
          </Item.Meta>
          <Item.Description>
            {description || 'No user info.'}
          </Item.Description>
          <div className='users-statistics'>

      </div>
        </Item.Content>
      </Item>
      <Divider/>
      <div>
        <Button>Force Refresh</Button>
      </div>
    </>
);

export default User;