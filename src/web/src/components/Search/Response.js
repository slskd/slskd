import React, { Component } from 'react';
import * as transfers from '../../lib/transfers';

import { formatBytes, getDirectoryName } from '../../lib/util';

import FileList from '../Shared/FileList'

import { 
    Button, 
    Card, 
    Icon,
    Label
} from 'semantic-ui-react';

const buildTree = (response) => {
    let { files = [], lockedFiles = [] } = response;
    files = files.concat(lockedFiles.map(file => ({ ...file, locked: true })));

    return files.reduce((dict, file) => {
        let dir = getDirectoryName(file.filename);
        let selectable = { selected: false, ...file };
        dict[dir] = dict[dir] === undefined ? [ selectable ] : dict[dir].concat(selectable);
        return dict;
    }, {});
}

class Response extends Component {
    state = { 
        tree: buildTree(this.props.response), 
        downloadRequest: undefined, 
        downloadError: '' 
    }

    componentDidUpdate = (prevProps) => {
        if (this.props.response !== prevProps.response) {
            this.setState({ tree: buildTree(this.props.response) });
        }
    }

    onFileSelectionChange = (file, state) => {
        file.selected = state;
        console.log(this.state.tree);
        this.setState({ tree: this.state.tree, downloadRequest: undefined, downloadError: '' })
    }

    download = (username, files) => {
        this.setState({ downloadRequest: 'inProgress' }, () => {
            Promise.all((files || []).map(f => this.downloadOne(username, f)))
            .then(() => this.setState({ downloadRequest: 'complete' }))
            .catch(err => this.setState({ downloadRequest: 'error', downloadError: err.response }))
        });
    }

    downloadOne = (username, file) => {
        const { filename, size } = file;
        return transfers.download({ username, filename, size });
    }

    render = () => {
        let response = this.props.response;
        let free = response.freeUploadSlots > 0;

        let { tree, downloadRequest, downloadError } = this.state;

        let selectedFiles = Object.keys(tree)
            .reduce((list, dict) => list.concat(tree[dict]), [])
            .filter(f => f.selected);

        let selectedSize = formatBytes(selectedFiles.reduce((total, f) => total + f.size, 0));

        return (
            <Card className='result-card' raised>
                <Card.Content>
                    <Card.Header>
                        <Icon name='circle' color={free ? 'green' : 'yellow'}/>
                        {response.username}
                        <Icon 
                            className='close-button' 
                            name='close' 
                            color='red' 
                            link
                            onClick={() => this.props.onHide()}
                        />
                    </Card.Header>
                    <Card.Meta className='result-meta'>
                        <span>Upload Speed: {formatBytes(response.uploadSpeed)}/s, Free Upload Slot: {free ? 'YES' : 'NO'}, Queue Length: {response.queueLength}</span>
                    </Card.Meta>
                    {(Object.keys(tree) || []).map((dir, i) => 
                        <FileList 
                            key={i}
                            directoryName={dir}
                            locked={tree[dir].find(file => file.locked)}
                            files={tree[dir]}
                            disabled={downloadRequest === 'inProgress'}
                            onSelectionChange={this.onFileSelectionChange}
                        />
                    )}
                </Card.Content>
                {selectedFiles.length > 0 && <Card.Content extra>
                        <span>
                            <Button 
                                color='green' 
                                content='Download'
                                icon='download' 
                                label={{ 
                                    as: 'a', 
                                    basic: false, 
                                    content: `${selectedFiles.length} file${selectedFiles.length === 1 ? '' : 's'}, ${selectedSize}`
                                }}
                                labelPosition='right'
                                onClick={() => this.download(response.username, selectedFiles)}
                                disabled={downloadRequest === 'inProgress'}
                            />
                            {downloadRequest === 'inProgress' && <Icon loading name='circle notch' size='large'/>}
                            {downloadRequest === 'complete' && <Icon name='checkmark' color='green' size='large'/>}
                            {downloadRequest === 'error' && <span>
                                <Icon name='x' color='red' size='large'/>
                                <Label>{downloadError.data + ` (HTTP ${downloadError.status} ${downloadError.statusText})`}</Label>
                            </span>}
                        </span>
                </Card.Content>}
            </Card>
        )
    }
}

export default Response;
