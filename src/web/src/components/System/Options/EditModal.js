import React, { useState, useEffect } from 'react';

import { Button, Icon, Message, Modal } from 'semantic-ui-react';
import CodeEditor from '../../Shared/CodeEditor';

import { getYamlLocation, getYaml, validateYaml, updateYaml } from '../../../lib/options';
import {
  PlaceholderSegment,
  Switch,
} from '../../Shared';

const EditModal = ({ open, onClose }) => {
  const [{ loading, error }, setLoading] = useState({ loading: true, error: false });
  const [{ location, yaml, isDirty }, setYaml] = useState({ location: undefined, yaml: undefined, isDirty: false });
  const [yamlError, setYamlError] = useState();
  const [updateError, setUpdateError] = useState();

  useEffect(() => {
    if (open) {
      get();
    }
  }, [open]);

  const get = async () => {
    setLoading({ loading: true, error: false });

    try {
      const [location, yaml] = await Promise.all([getYamlLocation(), getYaml()]);

      setYaml({ location, yaml, isDirty: false });
      setLoading({ loading: false, error: false });
    } catch (error) {
      setLoading({ loading: false, error: error.message });
    }
  };

  const update = async (yaml) => {
    setYaml({ location, yaml, isDirty: true });
    validate(yaml);
  };

  const validate = async (yaml) => {
    const response = await validateYaml({ yaml });
    setYamlError(response);
  };

  const save = async (yaml) => {
    await validate(yaml);

    if (!yamlError) {
      try {
        await updateYaml({ yaml });
        onClose();
      }
      catch (error) {
        setUpdateError(error.response.data);
      }
    }
  };

  return (
    <Modal
      size='large'
      open={open}
      onClose={onClose}
    >
      <Modal.Header>
        <Icon name='edit'/>
        Edit Options
        <Message className='no-grow edit-code-header' warning>
          <Icon name='warning sign'/>Editing {location}
        </Message>
      </Modal.Header>
      <Modal.Content className='edit-code-content' scrolling>
        <Switch
          loading={loading && <PlaceholderSegment loading={true}/>}
          error={error && <PlaceholderSegment icon='close'/>}
        >
          <div 
            {...{ className: (yamlError || updateError) ? 'edit-code-container-error' : 'edit-code-container' }} 
          >
            <CodeEditor
              value={yaml}
              onChange={(value) => update(value)}
            />
          </div>
        </Switch>
      </Modal.Content>
      <Modal.Actions>
        {(yamlError || updateError) &&
            <Message className='no-grow left-align' negative>
              <Icon name='x' />{(yamlError ?? '') + (updateError ?? '')}
            </Message>}
        <Button primary disabled={!isDirty} onClick={() => save(yaml)}><Icon name='save'/>Save</Button>
        <Button negative onClick={onClose}><Icon name='close'/>Cancel</Button>
      </Modal.Actions>
    </Modal>
  );
};

export default EditModal;