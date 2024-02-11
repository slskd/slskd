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
  // eslint-disable-next-line react/hook-use-state
  const [{ error, loading }, setLoading] = useState({
    error: false,
    loading: true,
  });
  // eslint-disable-next-line react/hook-use-state
  const [{ isDirty, location, yaml }, setYaml] = useState({
    isDirty: false,
    location: undefined,
    yaml: undefined,
  });
  const [yamlError, setYamlError] = useState();
  const [updateError, setUpdateError] = useState();

  const get = async () => {
    setLoading({ error: false, loading: true });

    try {
      const [locationResult, yamlResult] = await Promise.all([
        getYamlLocation(),
        getYaml(),
      ]);

      setYaml({ isDirty: false, location: locationResult, yaml: yamlResult });
      setLoading({ error: false, loading: false });
    } catch (getError) {
      setLoading({ error: getError.message, loading: false });
    }
  };

  const validate = async (newYaml) => {
    const response = await validateYaml({ yaml: newYaml });
    setYamlError(response);
  };

  const update = async (newYaml) => {
    setYaml({ isDirty: true, location, yaml: newYaml });
    validate(newYaml);
  };

  const save = async (newYaml) => {
    await validate(newYaml);

    if (!yamlError) {
      try {
        await updateYaml({ yaml: newYaml });
        onClose();
      } catch (nextUpdateError) {
        setUpdateError(nextUpdateError.response.data);
      }
    }
  };

  useEffect(() => {
    if (open) {
      get();
    }
  }, [open]);

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
