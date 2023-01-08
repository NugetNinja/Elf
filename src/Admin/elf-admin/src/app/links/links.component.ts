import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { Link, LinkService, PagedLinkResult } from './link.service';
import { MatTableDataSource } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';
import { EditLinkDialog } from './edit-link/edit-link-dialog';
import { ShareDialog } from './share/share-dialog';
import { environment } from 'src/environments/environment';
import { ConfirmationDialog } from '../shared/confirmation-dialog';
import { Clipboard } from '@angular/cdk/clipboard';
import { AppCacheService } from '../shared/appcache.service';
import { Tag, TagService } from '../tag/tag.service';
import { FormControl } from '@angular/forms';
import { map, Observable, startWith } from 'rxjs';
import { MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatChipInputEvent } from '@angular/material/chips';
import { MatSnackBar } from '@angular/material/snack-bar';
@Component({
    selector: 'app-links',
    templateUrl: './links.component.html',
    styleUrls: ['./links.component.css']
})
export class LinksComponent implements OnInit {
    addOnBlur = true;
    readonly separatorKeysCodes = [ENTER, COMMA] as const;
    tagCtrl = new FormControl();
    filteredTags: Observable<Tag[]>;
    linkId: number;

    ENV = environment;
    isLoading = false;
    totalRows = 0;
    pageSize = 10;
    currentPage = 0;
    searchTerm: string;
    queryTags: Tag[] = [];
    allTags: Tag[] = [];

    displayedColumns: string[] = ['fwToken', 'originUrl', 'note', 'akaName', 'tags', 'isEnabled', 'ttl', 'updateTimeUtc', 'action', 'manage'];
    dataSource: MatTableDataSource<Link> = new MatTableDataSource();
    @ViewChild('tagInput') tagInput: ElementRef;

    constructor(
        private _snackBar: MatSnackBar,
        public dialog: MatDialog,
        private clipboard: Clipboard,
        private appCache: AppCacheService,
        private linkService: LinkService,
        private tagService: TagService) {
        this.filteredTags = this.tagCtrl.valueChanges.pipe(
            startWith(null),
            map((tag: string | Tag | null) => tag ? this._filter(tag) : this.allTags.slice()));
    }

    ngOnInit(): void {
        this.updateTagCache();
        this.getLinks();
    }

    updateTagCache() {
        this.isLoading = true;
        this.tagService.list()
            .subscribe((result: Tag[]) => {
                this.isLoading = false;
                this.appCache.tags = result;
                this.allTags = result;
            });
    }

    addNewLink() {
        let diagRef = this.dialog.open(EditLinkDialog);
        diagRef.afterClosed().subscribe(result => {
            if (result) {
                this.getLinks();
                this.updateTagCache();
            }
        });
    }

    shareLink(link: Link) {
        this.dialog.open(ShareDialog, { data: link });
    }

    editLink(link: Link) {
        let diagRef = this.dialog.open(EditLinkDialog, { data: link });
        diagRef.afterClosed().subscribe(result => {
            if (result) {
                this.getLinks();
                this.updateTagCache();
            }
        });
    }

    search() {
        this.getLinks(true);
    }

    getLinks(reset: boolean = false): void {
        if (reset) {
            this.totalRows = 0;
            this.currentPage = 0;
        }

        this.isLoading = true;

        this.linkService.list(this.pageSize, this.currentPage * this.pageSize, this.searchTerm)
            .subscribe((result: PagedLinkResult) => {
                this.isLoading = false;
                this.dataSource = new MatTableDataSource(result.links);
            });
    }

    checkLink(id: number, isEnabled: boolean): void {
        this.linkService.setEnable(id, isEnabled).subscribe(() => {
            this._snackBar.open('Updated', 'OK', {
                duration: 3000
            });
        });
    }

    //#region Delete

    public deleteLinkDialogOpened: boolean = false;

    deleteLinkDialogClose() {
        this.linkId = null;
        this.deleteLinkDialogOpened = false;
    }

    showDeleteLink(link: Link): void {
        this.linkId = link?.id;
        this.deleteLinkDialogOpened = true;
    }

    deleteLink(): void {
        this.linkService.delete(this.linkId).subscribe(() => {
            this.getLinks();
        });
    }

    //#endregion

    copyChip(link: Link) {
        this.clipboard.copy(environment.elfApiBaseUrl + '/fw/' + link.fwToken);
        this._snackBar.open('Copied', 'Done', {
            duration: 3000
        });
    }

    copyAka(link: Link) {
        this.clipboard.copy(environment.elfApiBaseUrl + '/aka/' + link.akaName);
        this._snackBar.open('Aka url copied', 'Done', {
            duration: 3000
        });
    }

    //#region Query by Tags

    searchByTags() {
        if (this.queryTags.length == 0) {
            this.getLinks();
            return;
        }

        this.isLoading = true;
        this.linkService.listByTags(this.pageSize, this.currentPage * this.pageSize, this.queryTags.map(t => t.id))
            .subscribe((result: PagedLinkResult) => {
                this.isLoading = false;
                this.dataSource = new MatTableDataSource(result.links);
            });
    }

    tagClick(tag: Tag) {
        if (!this.queryTags.includes(tag)) {
            this.queryTags.push(tag);
        }
    }

    add(event: MatChipInputEvent): void {
        const value = event.value;

        if ((value || '').trim()) {
            var found = this.allTags.find(t => t.name.toLowerCase() == value);
            if (found && !this.queryTags.includes(found)) {
                this.queryTags.push(found);
            }
        }

        // Reset the input value
        event.chipInput!.clear();

        this.tagCtrl.setValue(null);
    }

    remove(tag: Tag, indx: number): void {
        this.queryTags.splice(indx, 1);
    }

    selected(event: MatAutocompleteSelectedEvent): void {
        if (!this.queryTags.includes(event.option.value)) {
            this.queryTags.push(event.option.value);
        }

        if (this.tagInput) this.tagInput.nativeElement.value = '';
        this.tagCtrl.setValue(null);
    }

    private _filter(value: string | Tag): Tag[] {
        var t = typeof (value);
        if (t == 'string') {
            return this.allTags.filter(tag => tag.name.toLowerCase().includes((value as string).toLowerCase()));
        }
        else {
            return this.allTags.filter(tag => tag.name.toLowerCase().includes((value as Tag).name.toLowerCase()));
        }
    }

    //#endregion
}