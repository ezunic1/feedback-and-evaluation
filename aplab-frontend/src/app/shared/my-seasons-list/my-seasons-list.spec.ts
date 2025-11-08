import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MySeasonsList } from './my-seasons-list';

describe('MySeasonsList', () => {
  let component: MySeasonsList;
  let fixture: ComponentFixture<MySeasonsList>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MySeasonsList]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MySeasonsList);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
