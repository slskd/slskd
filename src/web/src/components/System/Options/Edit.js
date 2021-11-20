import React, { useState, useEffect } from 'react';

import { Button, Icon, Message } from 'semantic-ui-react';
import CodeEditor from './CodeEditor';

import { getYaml, validateYaml, updateYaml } from '../../../lib/options';
import PlaceholderSegment from '../../Shared/PlaceholderSegment';

const Edit = ({ cancelAction }) => {
  const [{ loading, error }, setLoading] = useState({ loading: true, error: false });
  const [{ yaml, isDirty }, setYaml] = useState({ yaml: undefined, isDirty: false });
  const [yamlError, setYamlError] = useState();

  useEffect(() => {
    get();
  }, [])

  const get = async () => {
    setLoading({ loading: true, error: false });

    try {
      const yaml = await getYaml();
      setYaml({ yaml: yaml, isDirty: false })
      setLoading({ loading: false, error: false })
    } catch (error) {
      setLoading({ loading: false, error: error.message })
    }
  }

  const update = async (yaml) => {
    setYaml({ yaml, isDirty: true });
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
      <div 
        className='code-container' 
        style={{height: yamlError ? 'calc(100vh - 316px)' : undefined}}
      >
        <CodeEditor
          value={yaml}
          onChange={(value) => update(value)}
        />
      </div>
      {yamlError && <Message className='yaml-error' icon='x' negative>{yamlError}</Message>}
      <div className='footer-buttons'>
        <Button primary disabled={!isDirty} onClick={() => save(yaml)}><Icon name='save'/>Save</Button>
        <Button onClick={() => cancelAction()}><Icon name='close'/>Cancel</Button>
      </div>
    </>
  );
}

export default Edit;