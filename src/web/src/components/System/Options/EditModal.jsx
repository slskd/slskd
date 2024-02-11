import {
  getYaml,
  getYamlLocation,
  updateYaml,
  validateYaml,
} from '../../../lib/options';
import { Div, PlaceholderSegment, Switch } from '../../Shared';
import CodeEditor from '../../Shared/CodeEditor';
import React, { useEffect, useState } from 'react';
import { Button, Icon, Message, Modal } from 'semantic-ui-react';

const EditModal = ({ onClose, open, theme }) => {
  const [{ error, loading }, setLoading] = useState({
    error: false,
    loading: true,
  });
  const [{ isDirty, location, yaml }, setYaml] = useState({
    isDirty: false,
    location: undefined,
    yaml: undefined,
  });
  const [yamlError, setYamlError] = useState();
  const [updateError, setUpdateError] = useState();

  useEffect(() => {
    if (open) {
      get();
    }
  }, [open]);

  const get = async () => {
    setLoading({ error: false, loading: true });

    try {
      const [location, yaml] = await Promise.all([
        getYamlLocation(),
        getYaml(),
      ]);

      setYaml({ isDirty: false, location, yaml });
      setLoading({ error: false, loading: false });
    } catch (error) {
      setLoading({ error: error.message, loading: false });
    }
  };

  const update = async (yaml) => {
    setYaml({ isDirty: true, location, yaml });
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
      } catch (error) {
        setUpdateError(error.response.data);
      }
    }
  };

  return (
    <Modal
      onClose={onClose}
      open={open}
      size="large"
    >
      <Modal.Header>
        <Icon name="edit" />
        Edit Options
        <Div hidden={loading}>
          <Message
            className="no-grow edit-code-header"
            warning
          >
            <Icon name="warning sign" />
            Editing {location}
          </Message>
        </Div>
      </Modal.Header>
      <Modal.Content
        className="edit-code-content"
        scrolling
      >
        <Switch
          error={error && <PlaceholderSegment icon="close" />}
          loading={loading && <PlaceholderSegment loading />}
        >
          <div
            {...{
              className:
                yamlError || updateError
                  ? 'edit-code-container-error'
                  : 'edit-code-container',
            }}
          >
            <CodeEditor
              onChange={(value) => update(value)}
              style={{ minHeight: 500 }}
              theme={theme}
              value={yaml}
            />
          </div>
        </Switch>
      </Modal.Content>
      <Modal.Actions>
        {(yamlError || updateError) && (
          <Message
            className="no-grow left-align"
            negative
          >
            <Icon name="x" />
            {(yamlError ?? '') + (updateError ?? '')}
          </Message>
        )}
        <Button
          disabled={!isDirty}
          onClick={() => save(yaml)}
          primary
        >
          <Icon name="save" />
          Save
        </Button>
        <Button
          negative
          onClick={onClose}
        >
          <Icon name="close" />
          Cancel
        </Button>
      </Modal.Actions>
    </Modal>
  );
};

export default EditModal;
