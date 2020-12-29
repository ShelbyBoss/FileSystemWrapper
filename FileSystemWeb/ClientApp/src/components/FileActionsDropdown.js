﻿import React, {useState, useEffect} from 'react';
import {Link} from 'react-router-dom';
import {Dropdown, DropdownToggle, DropdownMenu, DropdownItem,} from 'reactstrap';
import {encodeBase64UnicodeCustom, getFileType} from '../Helpers/Path';
import store from '../Helpers/store'
import './FileActionsDropdown.css'

function formatFileSize(size) {
    const endings = ['B', 'kB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
    let ending = '';

    for (let i = 0; i < endings.length; i++) {
        ending = endings[i];
        if (size < 1024) break;

        size /= 1024;
    }

    return `${parseFloat(size.toPrecision(3))} ${ending}`;
}

function clickLink(e) {
    const linkElement = e.target.querySelector('a');
    if (linkElement) linkElement.click();
}

export default function (props) {
    const file = props.file;
    const fileOpenFileLink = `/file/view?path=${encodeURIComponent(file.path)}`;
    const fileOpenContentLink = `/api/files/${encodeBase64UnicodeCustom(file.path)}`;
    const fileDownloadLink = `/api/files/${encodeBase64UnicodeCustom(file.path)}/download`;
    const fileShareFileLink = file.sharedId ?
        `/share/file/edit/${encodeURIComponent(file.path)}` :
        `/share/file/add?path=${encodeURIComponent(file.path)}`;
    const isSupportedFile = ['image', 'audio', 'video', 'text', 'pdf'].includes(getFileType(file.extension));

    const [isLoggedIn, setIsLoggedIn] = useState(false);
    const [isLoggedInCallbackId, setIsLoggedInCallbackId] = useState(null);

    useEffect(() => {
        setIsLoggedInCallbackId(store.addCallback('isLoggedIn', value => setIsLoggedIn(value)));
        setIsLoggedIn(store.get('isLoggedIn'));
    }, []);
    useEffect(() => {
        return () => isLoggedInCallbackId && store.removeCallback('isLoggedIn', isLoggedInCallbackId)
    }, [isLoggedInCallbackId]);

    const [dropdownOpen, setDropdownOpen] = useState(false);
    const toggleDropdown = () => setDropdownOpen(prevState => !prevState);

    return (
        <Dropdown isOpen={dropdownOpen} toggle={toggleDropdown}>
            <DropdownToggle caret>
                {props.title || ''}
            </DropdownToggle>
            <DropdownMenu right>
                {!props.hideOpenFileLink ? (
                    <DropdownItem disabled={!file.permission.info} onClick={clickLink}>
                        <a href={fileOpenFileLink} target="_blank" rel="noopener noreferrer"
                           className={!file.permission.info ? 'text-secondary' : 'text-dark file-action-dropdown-link'}>
                            <i className="mr-2 fas fa-external-link-square-alt"/>
                            Open file in new tab
                        </a>
                    </DropdownItem>
                ) : null}
                <DropdownItem disabled={!file.permission.read || !isSupportedFile} onClick={clickLink}>
                    <a href={fileOpenContentLink} target="_blank" rel="noopener noreferrer"
                       className={!file.permission.read || !isSupportedFile ? 'text-secondary' : 'text-dark file-action-dropdown-link'}>
                        <i className="mr-2 fas fa-external-link-square-alt"/>
                        Open content in new tab
                    </a>
                </DropdownItem>
                <DropdownItem disabled={!file.permission.read} onClick={clickLink}>
                    <a href={fileDownloadLink} download={file.name}
                       className={!file.permission.read ? 'text-secondary' : 'text-dark file-action-dropdown-link'}>
                        <i className="mr-2 fas fa-download"/>
                        Download {file.size ? `(${formatFileSize(file.size)})` : ''}
                    </a>
                </DropdownItem>
                {isLoggedIn ? (
                    <DropdownItem disabled={!file.permission.info} onClick={clickLink}>
                        <Link to={fileShareFileLink}
                              className={!file.permission.info ? 'text-secondary' : 'text-dark file-action-dropdown-link'}>
                            <i className="mr-2 fas fa-share"/>
                            Share
                        </Link>
                    </DropdownItem>
                ) : null}
                {props.onDelete ? (
                    <DropdownItem disabled={!file.permission.write}
                                  onClick={() => props.onDelete(file)}>
                        <i className="mr-2 fas fa-trash"/>
                        Delete
                    </DropdownItem>
                ) : null}
            </DropdownMenu>
        </Dropdown>
    );
}