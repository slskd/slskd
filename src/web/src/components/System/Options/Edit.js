import React, { useState, useEffect } from 'react';

import { Button, Icon, Message } from 'semantic-ui-react';
import CodeEditor from '../../Shared/CodeEditor';

import { getYamlLocation, getYaml, validateYaml, updateYaml } from '../../../lib/options';
import PlaceholderSegment from '../../Shared/PlaceholderSegment';

const Edit = ({ cancelAction }) => {
  const [{ loading, error }, setLoading] = useState({ loading: true, error: false });
  const [{ location, yaml, isDirty }, setYaml] = useState({ location: undefined, yaml: undefined, isDirty: false });
  const [yamlError, setYamlError] = useState();

  useEffect(() => {
    get();
  }, [])

  const get = async () => {
    setLoading({ loading: true, error: false });

    try {
      const [location, yaml] = await Promise.all([getYamlLocation(), getYaml()]);

      setYaml({ location, yaml, isDirty: false })
      setLoading({ loading: false, error: false })
    } catch (error) {
      setLoading({ loading: false, error: error.message })
    }
  }

  const update = async (yaml) => {
    setYaml({ location, yaml, isDirty: true });
    validate(yaml);
  }

  const validate = async (yaml) => {
    const response = await validateYaml({ yaml });
    setYamlError(response);
  }

  const save = async (yaml) => {
    await validate(yaml);

    if (!yamlError) {
      await updateYaml({ yaml });
      cancelAction();
    }
  }

  if (loading) {
    return <PlaceholderSegment loading={true}/>
  }

  if (error) {
    return <PlaceholderSegment icon='close'/>
  }

  return (
    <>
      <Message warning>
        <Icon name='warning sign'/>Editing {location}
      </Message>
      <div 
        {...{ className: yamlError ? 'edit-code-container-error' : 'edit-code-container' }} 
      >
        <CodeEditor
          value={yaml}
          onChange={(value) => update(value)}
        />
      </div>
      {yamlError && <Message className='yaml-error' negative><Icon name='x'/>{yamlError}</Message>}
      <div className='footer-buttons'>
        <Button primary disabled={!isDirty} onClick={() => save(yaml)}><Icon name='save'/>Save</Button>
        <Button onClick={() => cancelAction()}><Icon name='close'/>Cancel</Button>
      </div>
    </>
  );
}

export default Edit;