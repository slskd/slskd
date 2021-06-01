import { 
  Item,
  Image
} from 'semantic-ui-react';

const User = ({ 
    username, 
    description, 
    hasFreeUploadSlot, 
    hasPicture, 
    picture, 
    queueLength, 
    uploadSlots, 
    updatedAt 
  }) => (
    <Item>
      <Item.Image size='tiny' src={`data:image;base64,${picture}`} />

      <Item.Content>
        <Item.Header as='a'>{username}</Item.Header>
        <Item.Description>
          {description}
        </Item.Description>
      </Item.Content>
    </Item>
);

export default User;